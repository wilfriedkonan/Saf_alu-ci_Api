using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Clients;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    public class DevisService
    {
        private readonly string _connectionString;

        public DevisService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Devis>> GetAllAsync()
        {
            var devisList = new List<Devis>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT d.*, c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale
                FROM Devis d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                ORDER BY d.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                devisList.Add(MapToDevis(reader));
            }

            return devisList;
        }

        public async Task<Devis?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT d.*, c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale
                FROM Devis d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                WHERE d.Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var devis = MapToDevis(reader);

                // Charger les lignes
                reader.Close();
                devis.Lignes = await GetLignesDevisAsync(conn, id);

                return devis;
            }

            return null;
        }

        public async Task<string> GenerateNumeroAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("sp_GenererNumeroDevis", conn);
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

        public async Task<int> CreateAsync(Devis devis)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Générer le numéro automatiquement
                if (string.IsNullOrEmpty(devis.Numero))
                {
                    devis.Numero = await GenerateNumeroWithTransactionAsync(conn, transaction);
                }

                // Créer le devis
                using var cmd = new SqlCommand(@"
                    INSERT INTO Devis (Numero, ClientId, Titre, Description, Statut, MontantHT, TauxTVA, MontantTTC,
                                     DateCreation, DateValidite, DateModification, Conditions, Notes, UtilisateurCreation)
                    VALUES (@Numero, @ClientId, @Titre, @Description, @Statut, @MontantHT, @TauxTVA, @MontantTTC,
                           @DateCreation, @DateValidite, @DateModification, @Conditions, @Notes, @UtilisateurCreation);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                AddDevisParameters(cmd, devis);
                var devisId = (int)await cmd.ExecuteScalarAsync();

                // Ajouter les lignes
                if (devis.Lignes != null && devis.Lignes.Any())
                {
                    await CreateLignesAsync(conn, transaction, devisId, devis.Lignes);
                }

                await transaction.CommitAsync();
                return devisId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateAsync(Devis devis)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Mettre à jour le devis
                using var cmd = new SqlCommand(@"
                    UPDATE Devis SET 
                        ClientId = @ClientId, Titre = @Titre, Description = @Description, Statut = @Statut,
                        DateValidite = @DateValidite, DateModification = @DateModification, 
                        Conditions = @Conditions, Notes = @Notes
                    WHERE Id = @Id", conn, transaction);

                cmd.Parameters.AddWithValue("@Id", devis.Id);
                AddDevisParameters(cmd, devis);
                await cmd.ExecuteNonQueryAsync();

                // Supprimer les anciennes lignes
                using var deleteLignesCmd = new SqlCommand("DELETE FROM LignesDevis WHERE DevisId = @DevisId", conn, transaction);
                deleteLignesCmd.Parameters.AddWithValue("@DevisId", devis.Id);
                await deleteLignesCmd.ExecuteNonQueryAsync();

                // Ajouter les nouvelles lignes
                if (devis.Lignes != null && devis.Lignes.Any())
                {
                    await CreateLignesAsync(conn, transaction, devis.Id, devis.Lignes);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateStatutAsync(int id, string nouveauStatut)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Devis SET 
                    Statut = @Statut, 
                    DateModification = @DateModification,
                    DateEnvoi = CASE WHEN @Statut = 'Envoye' THEN GETDATE() ELSE DateEnvoi END,
                    DateValidation = CASE WHEN @Statut = 'Valide' THEN GETDATE() ELSE DateValidation END
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Statut", nouveauStatut);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<string> GenerateNumeroWithTransactionAsync(SqlConnection conn, SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year.ToString();
            using var cmd = new SqlCommand($@"
                SELECT ISNULL(MAX(CAST(RIGHT(Numero, 4) AS INT)), 0) + 1
                FROM Devis 
                WHERE Numero LIKE 'DEV{annee}%'", conn, transaction);

            var prochainNumero = (int)await cmd.ExecuteScalarAsync();
            return $"DEV{annee}{prochainNumero:0000}";
        }

        private async Task CreateLignesAsync(SqlConnection conn, SqlTransaction transaction, int devisId, List<LigneDevis> lignes)
        {
            for (int i = 0; i < lignes.Count; i++)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO LignesDevis (DevisId, Ordre, Designation, Description, Quantite, Unite, PrixUnitaireHT)
                    VALUES (@DevisId, @Ordre, @Designation, @Description, @Quantite, @Unite, @PrixUnitaireHT)", conn, transaction);

                cmd.Parameters.AddWithValue("@DevisId", devisId);
                cmd.Parameters.AddWithValue("@Ordre", i + 1);
                cmd.Parameters.AddWithValue("@Designation", lignes[i].Designation);
                cmd.Parameters.AddWithValue("@Description", lignes[i].Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Quantite", lignes[i].Quantite);
                cmd.Parameters.AddWithValue("@Unite", lignes[i].Unite);
                cmd.Parameters.AddWithValue("@PrixUnitaireHT", lignes[i].PrixUnitaireHT);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<List<LigneDevis>> GetLignesDevisAsync(SqlConnection conn, int devisId)
        {
            var lignes = new List<LigneDevis>();

            using var cmd = new SqlCommand("SELECT * FROM LignesDevis WHERE DevisId = @DevisId ORDER BY Ordre", conn);
            cmd.Parameters.AddWithValue("@DevisId", devisId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lignes.Add(new LigneDevis
                {
                    Id = reader.GetInt32("Id"),
                    DevisId = reader.GetInt32("DevisId"),
                    Ordre = reader.GetInt32("Ordre"),
                    Designation = reader.GetString("Designation"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Quantite = reader.GetDecimal("Quantite"),
                    Unite = reader.GetString("Unite"),
                    PrixUnitaireHT = reader.GetDecimal("PrixUnitaireHT")
                });
            }

            return lignes;
        }

        private void AddDevisParameters(SqlCommand cmd, Devis devis)
        {
            cmd.Parameters.AddWithValue("@Numero", devis.Numero);
            cmd.Parameters.AddWithValue("@ClientId", devis.ClientId);
            cmd.Parameters.AddWithValue("@Titre", devis.Titre);
            cmd.Parameters.AddWithValue("@Description", devis.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Statut", devis.Statut);
            cmd.Parameters.AddWithValue("@MontantHT", devis.MontantHT);
            cmd.Parameters.AddWithValue("@TauxTVA", devis.TauxTVA);
            cmd.Parameters.AddWithValue("@MontantTTC", devis.MontantTTC);
            cmd.Parameters.AddWithValue("@DateCreation", devis.DateCreation);
            cmd.Parameters.AddWithValue("@DateValidite", devis.DateValidite ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@Conditions", devis.Conditions ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Notes", devis.Notes ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", devis.UtilisateurCreation);
        }

        private Devis MapToDevis(SqlDataReader reader)
        {
            return new Devis
            {
                Id = reader.GetInt32("Id"),
                Numero = reader.GetString("Numero"),
                ClientId = reader.GetInt32("ClientId"),
                Titre = reader.GetString("Titre"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                Statut = reader.GetString("Statut"),
                MontantHT = reader.GetDecimal("MontantHT"),
                TauxTVA = reader.GetDecimal("TauxTVA"),
                MontantTTC = reader.GetDecimal("MontantTTC"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateValidite = reader.IsDBNull("DateValidite") ? null : reader.GetDateTime("DateValidite"),
                DateEnvoi = reader.IsDBNull("DateEnvoi") ? null : reader.GetDateTime("DateEnvoi"),
                DateValidation = reader.IsDBNull("DateValidation") ? null : reader.GetDateTime("DateValidation"),
                DateModification = reader.GetDateTime("DateModification"),
                Conditions = reader.IsDBNull("Conditions") ? null : reader.GetString("Conditions"),
                Notes = reader.IsDBNull("Notes") ? null : reader.GetString("Notes"),
                CheminPDF = reader.IsDBNull("CheminPDF") ? null : reader.GetString("CheminPDF"),
                UtilisateurCreation = reader.GetInt32("UtilisateurCreation"),
                UtilisateurValidation = reader.IsDBNull("UtilisateurValidation") ? null : reader.GetInt32("UtilisateurValidation"),
                Client = new Client
                {
                    Id = reader.GetInt32("ClientId"),
                    Nom = reader.IsDBNull("ClientNom") ? "" : reader.GetString("ClientNom"),
                    Prenom = reader.IsDBNull("ClientPrenom") ? null : reader.GetString("ClientPrenom"),
                    RaisonSociale = reader.IsDBNull("RaisonSociale") ? null : reader.GetString("RaisonSociale")
                }
            };
        }
    }
}