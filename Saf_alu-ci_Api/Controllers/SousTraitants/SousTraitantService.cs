using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.SousTraitants
{
    public class SousTraitantService
    {
        private readonly string _connectionString;

        public SousTraitantService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Récupère tous les sous-traitants avec leurs spécialités
        /// </summary>
        public async Task<List<SousTraitant>> GetAllAsync()
        {
            var sousTraitants = new List<SousTraitant>();
            var sousTraitantsDict = new Dictionary<int, SousTraitant>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Étape 1: Récupérer tous les sous-traitants
            using (var cmd = new SqlCommand(@"
                SELECT * FROM SousTraitants 
                WHERE Actif = 1
                ORDER BY DateCreation DESC", conn))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var sousTraitant = MapToSousTraitant(reader);
                    sousTraitant.Specialites = new List<SousTraitantSpecialite>();
                    sousTraitantsDict[sousTraitant.Id] = sousTraitant;
                    sousTraitants.Add(sousTraitant);
                }
            }

            // Étape 2: Charger toutes les spécialités pour tous les sous-traitants en une seule requête
            if (sousTraitants.Any())
            {
                var ids = string.Join(",", sousTraitants.Select(st => st.Id));

                using var specialitesCmd = new SqlCommand($@"
                    SELECT sts.SousTraitantId, sts.SpecialiteId, sts.NiveauExpertise,
                           s.Id, s.Nom, s.Description, s.Couleur, s.Actif
                    FROM SousTraitantsSpecialites sts
                    INNER JOIN Specialites s ON sts.SpecialiteId = s.Id
                    WHERE sts.SousTraitantId IN ({ids}) AND s.Actif = 1
                    ORDER BY s.Nom", conn);

                using var specialitesReader = await specialitesCmd.ExecuteReaderAsync();
                while (await specialitesReader.ReadAsync())
                {
                    var sousTraitantId = specialitesReader.GetInt32(0);

                    if (sousTraitantsDict.TryGetValue(sousTraitantId, out var sousTraitant))
                    {
                        var specialite = new SousTraitantSpecialite
                        {
                            SousTraitantId = sousTraitantId,
                            SpecialiteId = specialitesReader.GetInt32(1),
                            NiveauExpertise = specialitesReader.GetInt32(2),
                            Specialite = new Specialite
                            {
                                Id = specialitesReader.GetInt32(3),
                                Nom = specialitesReader.GetString(4),
                                Description = specialitesReader.IsDBNull(5) ? null : specialitesReader.GetString(5),
                                Couleur = specialitesReader.GetString(6),
                                Actif = specialitesReader.GetBoolean(7)
                            }
                        };

                        sousTraitant.Specialites.Add(specialite);
                    }
                }
            }

            // Étape 3: Charger les évaluations pour tous les sous-traitants
            if (sousTraitants.Any())
            {
                var ids = string.Join(",", sousTraitants.Select(st => st.Id));

                using var evaluationsCmd = new SqlCommand($@"
                    SELECT * FROM EvaluationsSousTraitants
                    WHERE SousTraitantId IN ({ids})
                    ORDER BY DateEvaluation DESC", conn);

                using var evaluationsReader = await evaluationsCmd.ExecuteReaderAsync();
                while (await evaluationsReader.ReadAsync())
                {
                    var sousTraitantId = evaluationsReader.GetInt32(evaluationsReader.GetOrdinal("SousTraitantId"));

                    if (sousTraitantsDict.TryGetValue(sousTraitantId, out var sousTraitant))
                    {
                        if (sousTraitant.Evaluations == null)
                            sousTraitant.Evaluations = new List<EvaluationSousTraitant>();

                        var evaluation = new EvaluationSousTraitant
                        {
                            Id = evaluationsReader.GetInt32(evaluationsReader.GetOrdinal("Id")),
                            SousTraitantId = sousTraitantId,
                            ProjetId = evaluationsReader.GetInt32(evaluationsReader.GetOrdinal("ProjetId")),
                            EtapeProjetId = evaluationsReader.IsDBNull(evaluationsReader.GetOrdinal("EtapeProjetId"))
                                ? null
                                : evaluationsReader.GetInt32(evaluationsReader.GetOrdinal("EtapeProjetId")),
                            Note = evaluationsReader.GetInt32(evaluationsReader.GetOrdinal("Note")),
                            Commentaire = evaluationsReader.IsDBNull(evaluationsReader.GetOrdinal("Commentaire"))
                                ? null
                                : evaluationsReader.GetString(evaluationsReader.GetOrdinal("Commentaire")),
                            Criteres = evaluationsReader.IsDBNull(evaluationsReader.GetOrdinal("Criteres"))
                                ? null
                                : evaluationsReader.GetString(evaluationsReader.GetOrdinal("Criteres")),
                            DateEvaluation = evaluationsReader.GetDateTime(evaluationsReader.GetOrdinal("DateEvaluation")),
                            EvaluateurId = evaluationsReader.GetInt32(evaluationsReader.GetOrdinal("EvaluateurId"))
                        };

                        sousTraitant.Evaluations.Add(evaluation);
                    }
                }
            }

            return sousTraitants;
        }

        public async Task<SousTraitant?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM SousTraitants WHERE Id = @Id AND Actif = 1", conn);
            cmd.Parameters.AddWithValue("@Id", id);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var sousTraitant = MapToSousTraitant(reader);
                reader.Close();

                // Charger les spécialités
                sousTraitant.Specialites = await GetSpecialitesBySousTraitantAsync(conn, id);

                // Charger les évaluations
                sousTraitant.Evaluations = await GetEvaluationsBySousTraitantAsync(conn, id);

                return sousTraitant;
            }

            return null;
        }

        public async Task<int> CreateAsync(SousTraitant sousTraitant)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Créer le sous-traitant
                using var cmd = new SqlCommand(@"
                    INSERT INTO SousTraitants (Nom, RaisonSociale, Email, Telephone, Adresse,
                                             Ville, Ncc, NomContact,
                                             EmailContact, TelephoneContact, Certifications, DateCreation, DateModification, Actif, UtilisateurCreation)
                    VALUES (@Nom, @RaisonSociale, @Email, @Telephone, @Adresse,
                           @Ville, @Ncc, @NomContact,
                           @EmailContact, @TelephoneContact, @Certifications, @DateCreation, @DateModification, @Actif, @UtilisateurCreation);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                AddSousTraitantParameters(cmd, sousTraitant);
                var sousTraitantId = (int)await cmd.ExecuteScalarAsync();

                // Ajouter les spécialités
                if (sousTraitant.Specialites != null && sousTraitant.Specialites.Any())
                {
                    await AddSpecialitesAsync(conn, transaction, sousTraitantId, sousTraitant.Specialites);
                }

                await transaction.CommitAsync();
                return sousTraitantId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateAsync(SousTraitant sousTraitant)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Mettre à jour le sous-traitant
                using var cmd = new SqlCommand(@"
                    UPDATE SousTraitants SET 
                        Nom = @Nom, RaisonSociale = @RaisonSociale, Email = @Email, Telephone = @Telephone,
                        Adresse = @Adresse, Ville = @Ville, Ncc = @Ncc, NomContact = @NomContact,
                        EmailContact = @EmailContact, TelephoneContact = @TelephoneContact,
                        Certifications = @Certifications, DateModification = @DateModification
                    WHERE Id = @Id", conn, transaction);

                cmd.Parameters.AddWithValue("@Id", sousTraitant.Id);
                AddSousTraitantParameters(cmd, sousTraitant);
                await cmd.ExecuteNonQueryAsync();

                // Supprimer les anciennes spécialités
                using var deleteCmd = new SqlCommand("DELETE FROM SousTraitantsSpecialites WHERE SousTraitantId = @SousTraitantId", conn, transaction);
                deleteCmd.Parameters.AddWithValue("@SousTraitantId", sousTraitant.Id);
                await deleteCmd.ExecuteNonQueryAsync();

                // Ajouter les nouvelles spécialités
                if (sousTraitant.Specialites != null && sousTraitant.Specialites.Any())
                {
                    await AddSpecialitesAsync(conn, transaction, sousTraitant.Id, sousTraitant.Specialites);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("UPDATE SousTraitants SET Actif = 0, DateModification = @DateModification WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> CreateEvaluationAsync(EvaluationSousTraitant evaluation)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Créer l'évaluation
                using var cmd = new SqlCommand(@"
                    INSERT INTO EvaluationsSousTraitants (SousTraitantId, ProjetId, EtapeProjetId, Note, Commentaire, Criteres, DateEvaluation, EvaluateurId)
                    VALUES (@SousTraitantId, @ProjetId, @EtapeProjetId, @Note, @Commentaire, @Criteres, @DateEvaluation, @EvaluateurId);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                cmd.Parameters.AddWithValue("@SousTraitantId", evaluation.SousTraitantId);
                cmd.Parameters.AddWithValue("@ProjetId", evaluation.ProjetId);
                cmd.Parameters.AddWithValue("@EtapeProjetId", evaluation.EtapeProjetId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Note", evaluation.Note);
                cmd.Parameters.AddWithValue("@Commentaire", evaluation.Commentaire ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Criteres", evaluation.Criteres ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateEvaluation", evaluation.DateEvaluation);
                cmd.Parameters.AddWithValue("@EvaluateurId", evaluation.EvaluateurId);

                var evaluationId = (int)await cmd.ExecuteScalarAsync();

                // Mettre à jour la note moyenne du sous-traitant
                using var updateCmd = new SqlCommand("EXEC sp_MettreAJourNoteSousTraitant @SousTraitantId", conn, transaction);
                updateCmd.Parameters.AddWithValue("@SousTraitantId", evaluation.SousTraitantId);
                await updateCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return evaluationId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Specialite>> GetAllSpecialitesAsync()
        {
            var specialites = new List<Specialite>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM Specialites WHERE Actif = 1 ORDER BY Nom", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                specialites.Add(new Specialite
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Couleur = reader.GetString("Couleur"),
                    Actif = reader.GetBoolean("Actif")
                });
            }

            return specialites;
        }

        private async Task<List<SousTraitantSpecialite>> GetSpecialitesBySousTraitantAsync(SqlConnection conn, int sousTraitantId)
        {
            var specialites = new List<SousTraitantSpecialite>();

            using var cmd = new SqlCommand(@"
                SELECT sts.*, s.Nom, s.Description, s.Couleur, s.Actif
                FROM SousTraitantsSpecialites sts
                INNER JOIN Specialites s ON sts.SpecialiteId = s.Id
                WHERE sts.SousTraitantId = @SousTraitantId AND s.Actif = 1", conn);

            cmd.Parameters.AddWithValue("@SousTraitantId", sousTraitantId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                specialites.Add(new SousTraitantSpecialite
                {
                    SousTraitantId = reader.GetInt32("SousTraitantId"),
                    SpecialiteId = reader.GetInt32("SpecialiteId"),
                    NiveauExpertise = reader.GetInt32("NiveauExpertise"),
                    Specialite = new Specialite
                    {
                        Id = reader.GetInt32("SpecialiteId"),
                        Nom = reader.GetString("Nom"),
                        Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                        Couleur = reader.GetString("Couleur"),
                        Actif = reader.GetBoolean("Actif")
                    }
                });
            }

            return specialites;
        }

        private async Task<List<EvaluationSousTraitant>> GetEvaluationsBySousTraitantAsync(SqlConnection conn, int sousTraitantId)
        {
            var evaluations = new List<EvaluationSousTraitant>();

            using var cmd = new SqlCommand(@"
                SELECT * FROM EvaluationsSousTraitants
                WHERE SousTraitantId = @SousTraitantId
                ORDER BY DateEvaluation DESC", conn);

            cmd.Parameters.AddWithValue("@SousTraitantId", sousTraitantId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                evaluations.Add(new EvaluationSousTraitant
                {
                    Id = reader.GetInt32("Id"),
                    SousTraitantId = reader.GetInt32("SousTraitantId"),
                    ProjetId = reader.GetInt32("ProjetId"),
                    EtapeProjetId = reader.IsDBNull("EtapeProjetId") ? null : reader.GetInt32("EtapeProjetId"),
                    Note = reader.GetInt32("Note"),
                    Commentaire = reader.IsDBNull("Commentaire") ? null : reader.GetString("Commentaire"),
                    Criteres = reader.IsDBNull("Criteres") ? null : reader.GetString("Criteres"),
                    DateEvaluation = reader.GetDateTime("DateEvaluation"),
                    EvaluateurId = reader.GetInt32("EvaluateurId")
                });
            }

            return evaluations;
        }

        private async Task AddSpecialitesAsync(SqlConnection conn, SqlTransaction transaction, int sousTraitantId, List<SousTraitantSpecialite> specialites)
        {
            foreach (var specialite in specialites)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO SousTraitantsSpecialites (SousTraitantId, SpecialiteId, NiveauExpertise)
                    VALUES (@SousTraitantId, @SpecialiteId, @NiveauExpertise)", conn, transaction);

                cmd.Parameters.AddWithValue("@SousTraitantId", sousTraitantId);
                cmd.Parameters.AddWithValue("@SpecialiteId", specialite.SpecialiteId);
                cmd.Parameters.AddWithValue("@NiveauExpertise", specialite.NiveauExpertise);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private void AddSousTraitantParameters(SqlCommand cmd, SousTraitant sousTraitant)
        {
            cmd.Parameters.AddWithValue("@Nom", sousTraitant.Nom);
            cmd.Parameters.AddWithValue("@RaisonSociale", sousTraitant.RaisonSociale ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Email", sousTraitant.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Telephone", sousTraitant.Telephone ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", sousTraitant.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", sousTraitant.Ville ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ncc", sousTraitant.Ncc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NomContact", sousTraitant.NomContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailContact", sousTraitant.EmailContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TelephoneContact", sousTraitant.TelephoneContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Certifications", sousTraitant.Certifications ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", sousTraitant.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Actif", sousTraitant.Actif);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", sousTraitant.UtilisateurCreation ?? (object)DBNull.Value);
        }

        private SousTraitant MapToSousTraitant(SqlDataReader reader)
        {
            return new SousTraitant
            {
                Id = reader.GetInt32("Id"),
                Nom = reader.GetString("Nom"),
                RaisonSociale = reader.IsDBNull("RaisonSociale") ? null : reader.GetString("RaisonSociale"),
                Email = reader.IsDBNull("Email") ? null : reader.GetString("Email"),
                Telephone = reader.IsDBNull("Telephone") ? null : reader.GetString("Telephone"),
                Adresse = reader.IsDBNull("Adresse") ? null : reader.GetString("Adresse"),
                Ville = reader.IsDBNull("Ville") ? null : reader.GetString("Ville"),
                Ncc = reader.IsDBNull("Ncc") ? null : reader.GetString("Ncc"),
                NomContact = reader.IsDBNull("NomContact") ? null : reader.GetString("NomContact"),
                EmailContact = reader.IsDBNull("EmailContact") ? null : reader.GetString("EmailContact"),
                TelephoneContact = reader.IsDBNull("TelephoneContact") ? null : reader.GetString("TelephoneContact"),
                NoteMoyenne = reader.GetDecimal("NoteMoyenne"),
                NombreEvaluations = reader.GetInt32("NombreEvaluations"),
                Certifications = reader.IsDBNull("Certifications") ? null : reader.GetString("Certifications"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                Actif = reader.GetBoolean("Actif"),
                UtilisateurCreation = reader.IsDBNull("UtilisateurCreation") ? null : reader.GetInt32("UtilisateurCreation")
            };
        }
    }
}