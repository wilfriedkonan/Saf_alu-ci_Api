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

        public async Task<List<SousTraitant>> GetAllAsync()
        {
            var sousTraitants = new List<SousTraitant>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT st.*, 
                       STRING_AGG(s.Nom, ', ') as Specialites
                FROM SousTraitants st
                LEFT JOIN SousTraitantsSpecialites sts ON st.Id = sts.SousTraitantId
                LEFT JOIN Specialites s ON sts.SpecialiteId = s.Id
                WHERE st.Actif = 1
                GROUP BY st.Id, st.Nom, st.RaisonSociale, st.Email, st.Telephone, st.TelephoneMobile,
                         st.Adresse, st.CodePostal, st.Ville, st.Siret, st.NumeroTVA, st.NomContact,
                         st.PrenomContact, st.EmailContact, st.TelephoneContact, st.NoteMoyenne,
                         st.NombreEvaluations, st.AssuranceValide, st.DateExpirationAssurance,
                         st.NumeroAssurance, st.Certifications, st.DateCreation, st.DateModification,
                         st.Actif, st.UtilisateurCreation
                ORDER BY st.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sousTraitants.Add(MapToSousTraitant(reader));
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
                    INSERT INTO SousTraitants (Nom, RaisonSociale, Email, Telephone, TelephoneMobile, Adresse,
                                             CodePostal, Ville, Siret, NumeroTVA, NomContact, PrenomContact,
                                             EmailContact, TelephoneContact, AssuranceValide, DateExpirationAssurance,
                                             NumeroAssurance, Certifications, DateCreation, DateModification, Actif, UtilisateurCreation)
                    VALUES (@Nom, @RaisonSociale, @Email, @Telephone, @TelephoneMobile, @Adresse,
                           @CodePostal, @Ville, @Siret, @NumeroTVA, @NomContact, @PrenomContact,
                           @EmailContact, @TelephoneContact, @AssuranceValide, @DateExpirationAssurance,
                           @NumeroAssurance, @Certifications, @DateCreation, @DateModification, @Actif, @UtilisateurCreation);
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
                        TelephoneMobile = @TelephoneMobile, Adresse = @Adresse, CodePostal = @CodePostal,
                        Ville = @Ville, Siret = @Siret, NumeroTVA = @NumeroTVA, NomContact = @NomContact,
                        PrenomContact = @PrenomContact, EmailContact = @EmailContact, TelephoneContact = @TelephoneContact,
                        AssuranceValide = @AssuranceValide, DateExpirationAssurance = @DateExpirationAssurance,
                        NumeroAssurance = @NumeroAssurance, Certifications = @Certifications, DateModification = @DateModification
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
                SELECT sts.*, s.Nom, s.Description, s.Couleur
                FROM SousTraitantsSpecialites sts
                INNER JOIN Specialites s ON sts.SpecialiteId = s.Id
                WHERE sts.SousTraitantId = @SousTraitantId", conn);

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
                        Couleur = reader.GetString("Couleur")
                    }
                });
            }

            return specialites;
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
            cmd.Parameters.AddWithValue("@TelephoneMobile", sousTraitant.TelephoneMobile ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Adresse", sousTraitant.Adresse ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CodePostal", sousTraitant.CodePostal ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ville", sousTraitant.Ville ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Siret", sousTraitant.Siret ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NumeroTVA", sousTraitant.NumeroTVA ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NomContact", sousTraitant.NomContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PrenomContact", sousTraitant.PrenomContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailContact", sousTraitant.EmailContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TelephoneContact", sousTraitant.TelephoneContact ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AssuranceValide", sousTraitant.AssuranceValide);
            cmd.Parameters.AddWithValue("@DateExpirationAssurance", sousTraitant.DateExpirationAssurance ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NumeroAssurance", sousTraitant.NumeroAssurance ?? (object)DBNull.Value);
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
                TelephoneMobile = reader.IsDBNull("TelephoneMobile") ? null : reader.GetString("TelephoneMobile"),
                Adresse = reader.IsDBNull("Adresse") ? null : reader.GetString("Adresse"),
                CodePostal = reader.IsDBNull("CodePostal") ? null : reader.GetString("CodePostal"),
                Ville = reader.IsDBNull("Ville") ? null : reader.GetString("Ville"),
                Siret = reader.IsDBNull("Siret") ? null : reader.GetString("Siret"),
                NumeroTVA = reader.IsDBNull("NumeroTVA") ? null : reader.GetString("NumeroTVA"),
                NomContact = reader.IsDBNull("NomContact") ? null : reader.GetString("NomContact"),
                PrenomContact = reader.IsDBNull("PrenomContact") ? null : reader.GetString("PrenomContact"),
                EmailContact = reader.IsDBNull("EmailContact") ? null : reader.GetString("EmailContact"),
                TelephoneContact = reader.IsDBNull("TelephoneContact") ? null : reader.GetString("TelephoneContact"),
                NoteMoyenne = reader.GetDecimal("NoteMoyenne"),
                NombreEvaluations = reader.GetInt32("NombreEvaluations"),
                AssuranceValide = reader.GetBoolean("AssuranceValide"),
                DateExpirationAssurance = reader.IsDBNull("DateExpirationAssurance") ? null : reader.GetDateTime("DateExpirationAssurance"),
                NumeroAssurance = reader.IsDBNull("NumeroAssurance") ? null : reader.GetString("NumeroAssurance"),
                Certifications = reader.IsDBNull("Certifications") ? null : reader.GetString("Certifications"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                Actif = reader.GetBoolean("Actif"),
                UtilisateurCreation = reader.IsDBNull("UtilisateurCreation") ? null : reader.GetInt32("UtilisateurCreation")
            };
        }
    }
}