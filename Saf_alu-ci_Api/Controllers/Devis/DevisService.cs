// Controllers/Devis/DevisServiceComplet.cs
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
            cmd.CommandType = CommandType.StoredProcedure;

            var outputParam = new SqlParameter("@NouveauNumero", SqlDbType.NVarChar, 20)
            {
                Direction = ParameterDirection.Output
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
                        MontantHT = @MontantHT, TauxTVA = @TauxTVA, MontantTTC = @MontantTTC,
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

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Supprimer les lignes en premier (contrainte de clé étrangère)
                using var deleteLignesCmd = new SqlCommand("DELETE FROM LignesDevis WHERE DevisId = @DevisId", conn, transaction);
                deleteLignesCmd.Parameters.AddWithValue("@DevisId", id);
                await deleteLignesCmd.ExecuteNonQueryAsync();

                // Supprimer le devis
                using var deleteDevisCmd = new SqlCommand("DELETE FROM Devis WHERE Id = @Id", conn, transaction);
                deleteDevisCmd.Parameters.AddWithValue("@Id", id);
                await deleteDevisCmd.ExecuteNonQueryAsync();

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

        public async Task<RechercheDevisResult> RechercherAsync(RechercheDevisRequest request)
        {
            var devisList = new List<Devis>();
            var whereConditions = new List<string>();
            var parameters = new List<SqlParameter>();

            // Construire la clause WHERE dynamiquement
            if (!string.IsNullOrEmpty(request.Search))
            {
                whereConditions.Add("(d.Numero LIKE @Search OR d.Titre LIKE @Search OR c.Nom LIKE @Search OR c.RaisonSociale LIKE @Search)");
                parameters.Add(new SqlParameter("@Search", $"%{request.Search}%"));
            }

            if (!string.IsNullOrEmpty(request.Statut))
            {
                whereConditions.Add("d.Statut = @Statut");
                parameters.Add(new SqlParameter("@Statut", request.Statut));
            }

            if (request.ClientId.HasValue)
            {
                whereConditions.Add("d.ClientId = @ClientId");
                parameters.Add(new SqlParameter("@ClientId", request.ClientId.Value));
            }

            if (request.DateDebut.HasValue)
            {
                whereConditions.Add("d.DateCreation >= @DateDebut");
                parameters.Add(new SqlParameter("@DateDebut", request.DateDebut.Value));
            }

            if (request.DateFin.HasValue)
            {
                whereConditions.Add("d.DateCreation <= @DateFin");
                parameters.Add(new SqlParameter("@DateFin", request.DateFin.Value));
            }

            var whereClause = whereConditions.Any() ? $"WHERE {string.Join(" AND ", whereConditions)}" : "";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Compter le total
            var countQuery = $@"
                SELECT COUNT(*) 
                FROM Devis d 
                LEFT JOIN Clients c ON d.ClientId = c.Id 
                {whereClause}";

            using var countCmd = new SqlCommand(countQuery, conn);
            countCmd.Parameters.AddRange(parameters.ToArray());
            var total = (int)await countCmd.ExecuteScalarAsync();

            // Récupérer les données paginées
            var offset = (request.Page - 1) * request.Limit;
            var dataQuery = $@"
                SELECT d.*, c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale
                FROM Devis d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                {whereClause}
                ORDER BY d.DateCreation DESC
                OFFSET {offset} ROWS
                FETCH NEXT {request.Limit} ROWS ONLY";

            using var dataCmd = new SqlCommand(dataQuery, conn);
            dataCmd.Parameters.AddRange(parameters.Select(p => new SqlParameter(p.ParameterName, p.Value)).ToArray());

            using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                devisList.Add(MapToDevis(reader));
            }

            return new RechercheDevisResult
            {
                Devis = devisList,
                Total = total,
                Page = request.Page,
                TotalPages = (int)Math.Ceiling((double)total / request.Limit)
            };
        }

        public async Task<StatistiquesDevis> GetStatistiquesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    COUNT(*) as Total,
                    COUNT(CASE WHEN Statut = 'Brouillon' THEN 1 END) as Brouillon,
                    COUNT(CASE WHEN Statut = 'Envoye' THEN 1 END) as Envoye,
                    COUNT(CASE WHEN Statut = 'EnNegociation' THEN 1 END) as EnNegociation,
                    COUNT(CASE WHEN Statut = 'Valide' THEN 1 END) as Valide,
                    COUNT(CASE WHEN Statut = 'Refuse' THEN 1 END) as Refuse,
                    COUNT(CASE WHEN Statut = 'Expire' THEN 1 END) as Expire,
                    ISNULL(SUM(MontantTTC), 0) as MontantTotal,
                    ISNULL(SUM(CASE WHEN Statut = 'Valide' THEN MontantTTC ELSE 0 END), 0) as MontantValide
                FROM Devis", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new StatistiquesDevis
                {
                    Total = reader.GetInt32("Total"),
                    Brouillon = reader.GetInt32("Brouillon"),
                    Envoye = reader.GetInt32("Envoye"),
                    EnNegociation = reader.GetInt32("EnNegociation"),
                    Valide = reader.GetInt32("Valide"),
                    Refuse = reader.GetInt32("Refuse"),
                    Expire = reader.GetInt32("Expire"),
                    MontantTotal = reader.GetDecimal("MontantTotal"),
                    MontantValide = reader.GetDecimal("MontantValide")
                };
            }

            return new StatistiquesDevis();
        }

        public async Task<byte[]> GeneratePDFAsync(Devis devis)
        {
            // TODO: Implémenter la génération PDF avec une librairie comme iTextSharp ou DinkToPdf
            // Pour l'instant, retourner un PDF placeholder
            await Task.Delay(100); // Simuler le temps de génération

            // Placeholder - retourner un PDF vide
            var pdfContent = "PDF placeholder pour le devis " + devis.Numero;
            return System.Text.Encoding.UTF8.GetBytes(pdfContent);
        }

        // Méthodes privées helpers

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
                cmd.Parameters.AddWithValue("@Ordre", lignes[i].Ordre);
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

    // Classes pour la recherche et les statistiques
    public class RechercheDevisRequest
    {
        public string? Search { get; set; }
        public string? Statut { get; set; }
        public int? ClientId { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
    }

    public class RechercheDevisResult
    {
        public List<Devis> Devis { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
    }

    public class StatistiquesDevis
    {
        public int Total { get; set; }
        public int Brouillon { get; set; }
        public int Envoye { get; set; }
        public int EnNegociation { get; set; }
        public int Valide { get; set; }
        public int Refuse { get; set; }
        public int Expire { get; set; }
        public decimal MontantTotal { get; set; }
        public decimal MontantValide { get; set; }
    }
}