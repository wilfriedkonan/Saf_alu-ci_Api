using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Clients;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Projets
{
    public class ProjetService
    {
        private readonly string _connectionString;

        public ProjetService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Projet>> GetAllAsync()
        {
            var projets = new List<Projet>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT p.*, 
                       c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale as ClientRaisonSociale,
                       tp.Nom as TypeProjetNom, tp.Description as TypeProjetDescription, tp.Couleur as TypeProjetCouleur,
                       u.Prenom as ChefProjetPrenom, u.Nom as ChefProjetNom
                FROM Projets p
                LEFT JOIN Clients c ON p.ClientId = c.Id
                LEFT JOIN TypesProjets tp ON p.TypeProjetId = tp.Id
                LEFT JOIN Utilisateurs u ON p.ChefProjetId = u.Id
                WHERE p.Actif = 1
                ORDER BY p.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                projets.Add(MapToProjet(reader));
            }

            return projets;
        }

        public async Task<Projet?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT p.*, 
                       c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale as ClientRaisonSociale,
                       tp.Nom as TypeProjetNom, tp.Description as TypeProjetDescription, tp.Couleur as TypeProjetCouleur,
                       u.Prenom as ChefProjetPrenom, u.Nom as ChefProjetNom
                FROM Projets p
                LEFT JOIN Clients c ON p.ClientId = c.Id
                LEFT JOIN TypesProjets tp ON p.TypeProjetId = tp.Id
                LEFT JOIN Utilisateurs u ON p.ChefProjetId = u.Id
                WHERE p.Id = @Id AND p.Actif = 1", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var projet = MapToProjet(reader);
                reader.Close();

                // Charger les étapes
                projet.Etapes = await GetEtapesProjetAsync(conn, id);

                return projet;
            }

            return null;
        }

        public async Task<List<TypeProjet>> GetAllTypesAsync()
        {
            var types = new List<TypeProjet>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SELECT * FROM TypesProjets WHERE Actif = 1 ORDER BY Nom", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                types.Add(new TypeProjet
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Couleur = reader.GetString("Couleur"),
                    Actif = reader.GetBoolean("Actif")
                });
            }

            return types;
        }

        public async Task<string> GenerateNumeroAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("sp_GenererNumeroProjet", conn);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;

            var outputParam = new SqlParameter("@NouveauNumero", System.Data.SqlDbType.NVarChar, 20)
            {
                Direction = System.Data.ParameterDirection.Output
            };
            cmd.Parameters.Add(outputParam);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return outputParam.Value.ToString();
        }

        public async Task<int> CreateAsync(Projet projet)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Générer le numéro automatiquement
                if (string.IsNullOrEmpty(projet.Numero))
                {
                    projet.Numero = await GenerateNumeroWithTransactionAsync(conn, transaction);
                }

                // Créer le projet
                using var cmd = new SqlCommand(@"
                    INSERT INTO Projets (Numero, Nom, Description, ClientId, TypeProjetId, DevisId, Statut,
                                       DateDebut, DateFinPrevue, BudgetInitial, BudgetRevise, CoutReel,
                                       AdresseChantier, CodePostalChantier, VilleChantier, PourcentageAvancement,
                                       ChefProjetId, DateCreation, DateModification, UtilisateurCreation, Actif)
                    VALUES (@Numero, @Nom, @Description, @ClientId, @TypeProjetId, @DevisId, @Statut,
                           @DateDebut, @DateFinPrevue, @BudgetInitial, @BudgetRevise, @CoutReel,
                           @AdresseChantier, @CodePostalChantier, @VilleChantier, @PourcentageAvancement,
                           @ChefProjetId, @DateCreation, @DateModification, @UtilisateurCreation, @Actif);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                AddProjetParameters(cmd, projet);
                var projetId = (int)await cmd.ExecuteScalarAsync();

                // Ajouter les étapes
                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    await CreateEtapesAsync(conn, transaction, projetId, projet.Etapes);
                }

                await transaction.CommitAsync();
                return projetId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateAsync(Projet projet)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Mettre à jour le projet
                using var cmd = new SqlCommand(@"
                    UPDATE Projets SET 
                        Nom = @Nom, Description = @Description, ClientId = @ClientId, TypeProjetId = @TypeProjetId,
                        DevisId = @DevisId, Statut = @Statut, DateDebut = @DateDebut, DateFinPrevue = @DateFinPrevue,
                        BudgetInitial = @BudgetInitial, BudgetRevise = @BudgetRevise, CoutReel = @CoutReel,
                        AdresseChantier = @AdresseChantier, CodePostalChantier = @CodePostalChantier, VilleChantier = @VilleChantier,
                        PourcentageAvancement = @PourcentageAvancement, ChefProjetId = @ChefProjetId, DateModification = @DateModification
                    WHERE Id = @Id", conn, transaction);

                cmd.Parameters.AddWithValue("@Id", projet.Id);
                AddProjetParameters(cmd, projet);
                await cmd.ExecuteNonQueryAsync();

                // Supprimer les anciennes étapes
                using var deleteEtapesCmd = new SqlCommand("DELETE FROM EtapesProjets WHERE ProjetId = @ProjetId", conn, transaction);
                deleteEtapesCmd.Parameters.AddWithValue("@ProjetId", projet.Id);
                await deleteEtapesCmd.ExecuteNonQueryAsync();

                // Ajouter les nouvelles étapes
                if (projet.Etapes != null && projet.Etapes.Any())
                {
                    await CreateEtapesAsync(conn, transaction, projet.Id, projet.Etapes);
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
            using var cmd = new SqlCommand("UPDATE Projets SET Actif = 0, DateModification = @DateModification WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateStatutAsync(int id, string nouveauStatut)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Projets SET 
                    Statut = @Statut, 
                    DateModification = @DateModification,
                    DateFinRelle = CASE WHEN @Statut = 'Termine' THEN GETDATE() ELSE DateFinRelle END
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Statut", nouveauStatut);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAvancementAsync(int id, int pourcentageAvancement)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Projets SET 
                    PourcentageAvancement = @PourcentageAvancement,
                    Statut = CASE 
                        WHEN @PourcentageAvancement = 100 THEN 'Termine'
                        WHEN @PourcentageAvancement > 0 THEN 'EnCours'
                        ELSE Statut
                    END,
                    DateModification = @DateModification,
                    DateFinRelle = CASE WHEN @PourcentageAvancement = 100 THEN GETDATE() ELSE DateFinRelle END
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@PourcentageAvancement", pourcentageAvancement);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<EtapeProjet>> GetEtapesProjetAsync(int projetId)
        {
            using var conn = new SqlConnection(_connectionString);
            return await GetEtapesProjetAsync(conn, projetId);
        }

        public async Task UpdateEtapeAvancementAsync(int etapeId, UpdateAvancementRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE EtapesProjets SET 
                    PourcentageAvancement = @PourcentageAvancement,
                    Statut = CASE 
                        WHEN @PourcentageAvancement = 100 THEN 'Termine'
                        WHEN @PourcentageAvancement > 0 THEN 'EnCours'
                        ELSE Statut
                    END,
                    DateFinReelle = CASE WHEN @PourcentageAvancement = 100 THEN GETDATE() ELSE DateFinReelle END
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", etapeId);
            cmd.Parameters.AddWithValue("@PourcentageAvancement", request.PourcentageAvancement);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            // Si une note est fournie, créer une évaluation
            if (request.Note.HasValue && request.Note.Value >= 1 && request.Note.Value <= 5)
            {
                // TODO: Implémenter la création d'évaluation
                // Nécessite de récupérer les infos de l'étape (ResponsableId, TypeResponsable)
            }
        }

        public async Task<List<Projet>> GetProjetsEnRetardAsync()
        {
            var projets = new List<Projet>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT p.*, 
                       c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale as ClientRaisonSociale,
                       tp.Nom as TypeProjetNom
                FROM Projets p
                LEFT JOIN Clients c ON p.ClientId = c.Id
                LEFT JOIN TypesProjets tp ON p.TypeProjetId = tp.Id
                WHERE p.Actif = 1 
                    AND p.Statut IN ('EnCours', 'Planification')
                    AND p.DateFinPrevue < GETDATE()
                    AND p.PourcentageAvancement < 100
                ORDER BY p.DateFinPrevue", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                projets.Add(MapToProjet(reader));
            }

            return projets;
        }

        private async Task<string> GenerateNumeroWithTransactionAsync(SqlConnection conn, SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year.ToString();
            using var cmd = new SqlCommand($@"
                SELECT ISNULL(MAX(CAST(RIGHT(Numero, 4) AS INT)), 0) + 1
                FROM Projets 
                WHERE Numero LIKE 'PRJ{annee}%'", conn, transaction);

            var prochainNumero = (int)await cmd.ExecuteScalarAsync();
            return $"PRJ{annee}{prochainNumero:0000}";
        }

        private async Task CreateEtapesAsync(SqlConnection conn, SqlTransaction transaction, int projetId, List<EtapeProjet> etapes)
        {
            for (int i = 0; i < etapes.Count; i++)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO EtapesProjets (ProjetId, Nom, Description, Ordre, DateDebut, DateFinPrevue, Statut,
                                             PourcentageAvancement, BudgetPrevu, CoutReel, ResponsableId, TypeResponsable)
                    VALUES (@ProjetId, @Nom, @Description, @Ordre, @DateDebut, @DateFinPrevue, @Statut,
                           @PourcentageAvancement, @BudgetPrevu, @CoutReel, @ResponsableId, @TypeResponsable)", conn, transaction);

                cmd.Parameters.AddWithValue("@ProjetId", projetId);
                cmd.Parameters.AddWithValue("@Nom", etapes[i].Nom);
                cmd.Parameters.AddWithValue("@Description", etapes[i].Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ordre", i + 1);
                cmd.Parameters.AddWithValue("@DateDebut", etapes[i].DateDebut ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateFinPrevue", etapes[i].DateFinPrevue ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Statut", etapes[i].Statut);
                cmd.Parameters.AddWithValue("@PourcentageAvancement", etapes[i].PourcentageAvancement);
                cmd.Parameters.AddWithValue("@BudgetPrevu", etapes[i].BudgetPrevu);
                cmd.Parameters.AddWithValue("@CoutReel", etapes[i].CoutReel);
                cmd.Parameters.AddWithValue("@ResponsableId", etapes[i].ResponsableId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@TypeResponsable", etapes[i].TypeResponsable);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<List<EtapeProjet>> GetEtapesProjetAsync(SqlConnection conn, int projetId)
        {
            var etapes = new List<EtapeProjet>();

            using var cmd = new SqlCommand(@"
                SELECT ep.*, u.Prenom + ' ' + u.Nom as ResponsableNom,
                       st.Nom as SousTraitantNom
                FROM EtapesProjets ep
                LEFT JOIN Utilisateurs u ON ep.ResponsableId = u.Id AND ep.TypeResponsable = 'Interne'
                LEFT JOIN SousTraitants st ON ep.ResponsableId = st.Id AND ep.TypeResponsable = 'SousTraitant'
                WHERE ep.ProjetId = @ProjetId 
                ORDER BY ep.Ordre", conn);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                etapes.Add(new EtapeProjet
                {
                    Id = reader.GetInt32("Id"),
                    ProjetId = reader.GetInt32("ProjetId"),
                    Nom = reader.GetString("Nom"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Ordre = reader.GetInt32("Ordre"),
                    DateDebut = reader.IsDBNull("DateDebut") ? null : reader.GetDateTime("DateDebut"),
                    DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                    DateFinReelle = reader.IsDBNull("DateFinReelle") ? null : reader.GetDateTime("DateFinReelle"),
                    Statut = reader.GetString("Statut"),
                    PourcentageAvancement = reader.GetInt32("PourcentageAvancement"),
                    BudgetPrevu = reader.GetDecimal("BudgetPrevu"),
                    CoutReel = reader.GetDecimal("CoutReel"),
                    ResponsableId = reader.IsDBNull("ResponsableId") ? null : reader.GetInt32("ResponsableId"),
                    TypeResponsable = reader.GetString("TypeResponsable")
                });
            }

            return etapes;
        }

        private void AddProjetParameters(SqlCommand cmd, Projet projet)
        {
            cmd.Parameters.AddWithValue("@Numero", projet.Numero);
            cmd.Parameters.AddWithValue("@Nom", projet.Nom);
            cmd.Parameters.AddWithValue("@Description", projet.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientId", projet.ClientId);
            cmd.Parameters.AddWithValue("@TypeProjetId", projet.TypeProjetId);
            cmd.Parameters.AddWithValue("@DevisId", projet.DevisId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Statut", projet.Statut);
            cmd.Parameters.AddWithValue("@DateDebut", projet.DateDebut ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateFinPrevue", projet.DateFinPrevue ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BudgetInitial", projet.BudgetInitial);
            cmd.Parameters.AddWithValue("@BudgetRevise", projet.BudgetRevise);
            cmd.Parameters.AddWithValue("@CoutReel", projet.CoutReel);
            cmd.Parameters.AddWithValue("@AdresseChantier", projet.AdresseChantier ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CodePostalChantier", projet.CodePostalChantier ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@VilleChantier", projet.VilleChantier ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PourcentageAvancement", projet.PourcentageAvancement);
            cmd.Parameters.AddWithValue("@ChefProjetId", projet.ChefProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", projet.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", projet.UtilisateurCreation);
            cmd.Parameters.AddWithValue("@Actif", projet.Actif);
        }

        private Projet MapToProjet(SqlDataReader reader)
        {
            return new Projet
            {
                Id = reader.GetInt32("Id"),
                Numero = reader.GetString("Numero"),
                Nom = reader.GetString("Nom"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                ClientId = reader.GetInt32("ClientId"),
                TypeProjetId = reader.GetInt32("TypeProjetId"),
                DevisId = reader.IsDBNull("DevisId") ? null : reader.GetInt32("DevisId"),
                Statut = reader.GetString("Statut"),
                DateDebut = reader.IsDBNull("DateDebut") ? null : reader.GetDateTime("DateDebut"),
                DateFinPrevue = reader.IsDBNull("DateFinPrevue") ? null : reader.GetDateTime("DateFinPrevue"),
                DateFinRelle = reader.IsDBNull("DateFinRelle") ? null : reader.GetDateTime("DateFinRelle"),
                BudgetInitial = reader.GetDecimal("BudgetInitial"),
                BudgetRevise = reader.GetDecimal("BudgetRevise"),
                CoutReel = reader.GetDecimal("CoutReel"),
                AdresseChantier = reader.IsDBNull("AdresseChantier") ? null : reader.GetString("AdresseChantier"),
                CodePostalChantier = reader.IsDBNull("CodePostalChantier") ? null : reader.GetString("CodePostalChantier"),
                VilleChantier = reader.IsDBNull("VilleChantier") ? null : reader.GetString("VilleChantier"),
                PourcentageAvancement = reader.GetInt32("PourcentageAvancement"),
                ChefProjetId = reader.IsDBNull("ChefProjetId") ? null : reader.GetInt32("ChefProjetId"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateModification = reader.GetDateTime("DateModification"),
                UtilisateurCreation = reader.GetInt32("UtilisateurCreation"),
                Actif = reader.GetBoolean("Actif"),
                Client = new Client
                {
                    Id = reader.GetInt32("ClientId"),
                    Nom = reader.IsDBNull("ClientNom") ? "" : reader.GetString("ClientNom"),
                    Prenom = reader.IsDBNull("ClientPrenom") ? null : reader.GetString("ClientPrenom"),
                    RaisonSociale = reader.IsDBNull("ClientRaisonSociale") ? null : reader.GetString("ClientRaisonSociale")
                },
                TypeProjet = new TypeProjet
                {
                    Id = reader.GetInt32("TypeProjetId"),
                    Nom = reader.IsDBNull("TypeProjetNom") ? "" : reader.GetString("TypeProjetNom"),
                    Description = reader.IsDBNull("TypeProjetDescription") ? null : reader.GetString("TypeProjetDescription"),
                    Couleur = reader.IsDBNull("TypeProjetCouleur") ? "#2563eb" : reader.GetString("TypeProjetCouleur")
                },
                ChefProjet = reader.IsDBNull("ChefProjetPrenom") ? null : new Utilisateur
                {
                    Id = reader.GetInt32("ChefProjetId"),
                    Prenom = reader.GetString("ChefProjetPrenom"),
                    Nom = reader.GetString("ChefProjetNom")
                }
            };
        }
    }
}
