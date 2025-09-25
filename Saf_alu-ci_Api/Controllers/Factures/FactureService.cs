using Microsoft.Data.SqlClient;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Factures
{
    public class FactureService
    {
        private readonly string _connectionString;

        public FactureService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<Facture>> GetAllAsync()
        {
            var factures = new List<Facture>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT f.*, 
                       c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale as ClientRaisonSociale,
                       st.Nom as SousTraitantNom, st.RaisonSociale as SousTraitantRaisonSociale
                FROM Factures f
                LEFT JOIN Clients c ON f.ClientId = c.Id
                LEFT JOIN SousTraitants st ON f.SousTraitantId = st.Id
                ORDER BY f.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                factures.Add(MapToFacture(reader));
            }

            return factures;
        }

        public async Task<Facture?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT f.*, 
                       c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale as ClientRaisonSociale,
                       st.Nom as SousTraitantNom, st.RaisonSociale as SousTraitantRaisonSociale
                FROM Factures f
                LEFT JOIN Clients c ON f.ClientId = c.Id
                LEFT JOIN SousTraitants st ON f.SousTraitantId = st.Id
                WHERE f.Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var facture = MapToFacture(reader);
                reader.Close();

                // Charger les lignes et échéanciers
                facture.Lignes = await GetLignesFactureAsync(conn, id);
                facture.Echeanciers = await GetEcheanciersAsync(conn, id);

                return facture;
            }

            return null;
        }

        public async Task<string> GenerateNumeroAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("sp_GenererNumeroFacture", conn);
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

        public async Task<int> CreateAsync(Facture facture)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Générer le numéro automatiquement
                if (string.IsNullOrEmpty(facture.Numero))
                {
                    facture.Numero = await GenerateNumeroWithTransactionAsync(conn, transaction);
                }

                // Créer la facture
                using var cmd = new SqlCommand(@"
                    INSERT INTO Factures (Numero, TypeFacture, ClientId, SousTraitantId, DevisId, ProjetId, Titre, Description,
                                        Statut, MontantHT, TauxTVA, MontantTVA, MontantTTC, MontantPaye, DateCreation, DateFacture,
                                        DateEcheance, DateModification, ConditionsPaiement, ReferenceClient, UtilisateurCreation)
                    VALUES (@Numero, @TypeFacture, @ClientId, @SousTraitantId, @DevisId, @ProjetId, @Titre, @Description,
                           @Statut, @MontantHT, @TauxTVA, @MontantTVA, @MontantTTC, @MontantPaye, @DateCreation, @DateFacture,
                           @DateEcheance, @DateModification, @ConditionsPaiement, @ReferenceClient, @UtilisateurCreation);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                AddFactureParameters(cmd, facture);
                var factureId = (int)await cmd.ExecuteScalarAsync();

                // Ajouter les lignes
                if (facture.Lignes != null && facture.Lignes.Any())
                {
                    await CreateLignesFactureAsync(conn, transaction, factureId, facture.Lignes);
                }

                // Ajouter les échéanciers
                if (facture.Echeanciers != null && facture.Echeanciers.Any())
                {
                    await CreateEcheanciersAsync(conn, transaction, factureId, facture.Echeanciers);
                }

                await transaction.CommitAsync();
                return factureId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task UpdateAsync(Facture facture)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Mise à jour de la facture principale
                using var cmd = new SqlCommand(@"
            UPDATE Factures SET 
                Titre = @Titre,
                Description = @Description,
                DateFacture = @DateFacture,
                DateEcheance = @DateEcheance,
                ConditionsPaiement = @ConditionsPaiement,
                ReferenceClient = @ReferenceClient,
                MontantHT = @MontantHT,
                MontantTVA = @MontantTVA,
                MontantTTC = @MontantTTC,
                DateModification = @DateModification
            WHERE Id = @Id", conn, transaction);

                cmd.Parameters.AddWithValue("@Id", facture.Id);
                cmd.Parameters.AddWithValue("@Titre", facture.Titre);
                cmd.Parameters.AddWithValue("@Description", facture.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateFacture", facture.DateFacture);
                cmd.Parameters.AddWithValue("@DateEcheance", facture.DateEcheance);
                cmd.Parameters.AddWithValue("@ConditionsPaiement", facture.ConditionsPaiement);
                cmd.Parameters.AddWithValue("@ReferenceClient", facture.ReferenceClient ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MontantHT", facture.MontantHT);
                cmd.Parameters.AddWithValue("@MontantTVA", facture.MontantTVA);
                cmd.Parameters.AddWithValue("@MontantTTC", facture.MontantTTC);
                cmd.Parameters.AddWithValue("@DateModification", facture.DateModification);

                await cmd.ExecuteNonQueryAsync();

                // Si des lignes sont fournies, les remplacer complètement
                if (facture.Lignes != null)
                {
                    // Supprimer les anciennes lignes
                    using var deleteLignesCmd = new SqlCommand(
                        "DELETE FROM LignesFactures WHERE FactureId = @FactureId",
                        conn, transaction);
                    deleteLignesCmd.Parameters.AddWithValue("@FactureId", facture.Id);
                    await deleteLignesCmd.ExecuteNonQueryAsync();

                    // Ajouter les nouvelles lignes
                    if (facture.Lignes.Any())
                    {
                        await CreateLignesFactureAsync(conn, transaction, facture.Id, facture.Lignes);
                    }
                }

                // Si des échéanciers sont fournis, les remplacer complètement
                if (facture.Echeanciers != null)
                {
                    // Supprimer les anciens échéanciers
                    using var deleteEcheanciersCmd = new SqlCommand(
                        "DELETE FROM Echeanciers WHERE FactureId = @FactureId",
                        conn, transaction);
                    deleteEcheanciersCmd.Parameters.AddWithValue("@FactureId", facture.Id);
                    await deleteEcheanciersCmd.ExecuteNonQueryAsync();

                    // Ajouter les nouveaux échéanciers
                    if (facture.Echeanciers.Any())
                    {
                        await CreateEcheanciersAsync(conn, transaction, facture.Id, facture.Echeanciers);
                    }
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
                UPDATE Factures SET 
                    Statut = @Statut, 
                    DateModification = @DateModification,
                    DateEnvoi = CASE WHEN @Statut = 'Envoyee' THEN GETDATE() ELSE DateEnvoi END,
                    DatePaiement = CASE WHEN @Statut = 'Payee' THEN GETDATE() ELSE DatePaiement END
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Statut", nouveauStatut);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarquerPayeAsync(int id, MarquerPayeRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Factures SET 
                    MontantPaye = @MontantPaye,
                    Statut = CASE WHEN (@MontantPaye >= MontantTTC) THEN 'Payee' ELSE 'Envoyee' END,
                    DatePaiement = @DatePaiement,
                    ModePaiement = @ModePaiement,
                    DateModification = @DateModification
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@MontantPaye", request.MontantPaye);
            cmd.Parameters.AddWithValue("@DatePaiement", request.DatePaiement);
            cmd.Parameters.AddWithValue("@ModePaiement", request.ModePaiement ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Facture?> CreateFromDevisAsync(int devisId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Récupérer le devis
                using var devisCmd = new SqlCommand(@"
                    SELECT d.*, c.Nom as ClientNom, c.Prenom as ClientPrenom, c.RaisonSociale
                    FROM Devis d
                    LEFT JOIN Clients c ON d.ClientId = c.Id
                    WHERE d.Id = @DevisId", conn, transaction);
                devisCmd.Parameters.AddWithValue("@DevisId", devisId);

                using var devisReader = await devisCmd.ExecuteReaderAsync();
                if (!await devisReader.ReadAsync())
                {
                    throw new Exception("Devis non trouvé");
                }

                var facture = new Facture
                {
                    TypeFacture = "Devis",
                    ClientId = devisReader.GetInt32("ClientId"),
                    DevisId = devisId,
                    Titre = $"Facture - {devisReader.GetString("Titre")}",
                    Description = devisReader.IsDBNull("Description") ? null : devisReader.GetString("Description"),
                    Statut = "Brouillon",
                    MontantHT = devisReader.GetDecimal("MontantHT"),
                    TauxTVA = devisReader.GetDecimal("TauxTVA"),
                    MontantTVA = devisReader.GetDecimal("MontantHT") * devisReader.GetDecimal("TauxTVA") / 100,
                    MontantTTC = devisReader.GetDecimal("MontantTTC"),
                    DateCreation = DateTime.UtcNow,
                    DateFacture = DateTime.UtcNow.Date,
                    DateEcheance = DateTime.UtcNow.Date.AddDays(30),
                    DateModification = DateTime.UtcNow,
                    ConditionsPaiement = "30 jours",
                    UtilisateurCreation = 1 // TODO: depuis JWT
                };

                devisReader.Close();

                // Générer le numéro
                facture.Numero = await GenerateNumeroWithTransactionAsync(conn, transaction);

                // Créer la facture
                using var factureCmd = new SqlCommand(@"
                    INSERT INTO Factures (Numero, TypeFacture, ClientId, DevisId, Titre, Description, Statut,
                                        MontantHT, TauxTVA, MontantTVA, MontantTTC, MontantPaye, DateCreation, DateFacture,
                                        DateEcheance, DateModification, ConditionsPaiement, UtilisateurCreation)
                    VALUES (@Numero, @TypeFacture, @ClientId, @DevisId, @Titre, @Description, @Statut,
                           @MontantHT, @TauxTVA, @MontantTVA, @MontantTTC, @MontantPaye, @DateCreation, @DateFacture,
                           @DateEcheance, @DateModification, @ConditionsPaiement, @UtilisateurCreation);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                AddFactureParameters(factureCmd, facture);
                var factureId = (int)await factureCmd.ExecuteScalarAsync();
                facture.Id = factureId;

                // Copier les lignes du devis
                using var lignesCmd = new SqlCommand(@"
                    INSERT INTO LignesFactures (FactureId, Ordre, Designation, Description, Quantite, Unite, PrixUnitaireHT)
                    SELECT @FactureId, Ordre, Designation, Description, Quantite, Unite, PrixUnitaireHT
                    FROM LignesDevis
                    WHERE DevisId = @DevisId
                    ORDER BY Ordre", conn, transaction);

                lignesCmd.Parameters.AddWithValue("@FactureId", factureId);
                lignesCmd.Parameters.AddWithValue("@DevisId", devisId);
                await lignesCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return facture;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<string> GenerateNumeroWithTransactionAsync(SqlConnection conn, SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year.ToString();
            using var cmd = new SqlCommand($@"
                SELECT ISNULL(MAX(CAST(RIGHT(Numero, 4) AS INT)), 0) + 1
                FROM Factures 
                WHERE Numero LIKE 'FAC{annee}%'", conn, transaction);

            var prochainNumero = (int)await cmd.ExecuteScalarAsync();
            return $"FAC{annee}{prochainNumero:0000}";
        }

        private async Task CreateLignesFactureAsync(SqlConnection conn, SqlTransaction transaction, int factureId, List<LigneFacture> lignes)
        {
            for (int i = 0; i < lignes.Count; i++)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO LignesFactures (FactureId, Ordre, Designation, Description, Quantite, Unite, PrixUnitaireHT)
                    VALUES (@FactureId, @Ordre, @Designation, @Description, @Quantite, @Unite, @PrixUnitaireHT)", conn, transaction);

                cmd.Parameters.AddWithValue("@FactureId", factureId);
                cmd.Parameters.AddWithValue("@Ordre", i + 1);
                cmd.Parameters.AddWithValue("@Designation", lignes[i].Designation);
                cmd.Parameters.AddWithValue("@Description", lignes[i].Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Quantite", lignes[i].Quantite);
                cmd.Parameters.AddWithValue("@Unite", lignes[i].Unite);
                cmd.Parameters.AddWithValue("@PrixUnitaireHT", lignes[i].PrixUnitaireHT);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task CreateEcheanciersAsync(SqlConnection conn, SqlTransaction transaction, int factureId, List<Echeancier> echeanciers)
        {
            for (int i = 0; i < echeanciers.Count; i++)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO Echeanciers (FactureId, Ordre, Description, MontantTTC, DateEcheance, Statut)
                    VALUES (@FactureId, @Ordre, @Description, @MontantTTC, @DateEcheance, @Statut)", conn, transaction);

                cmd.Parameters.AddWithValue("@FactureId", factureId);
                cmd.Parameters.AddWithValue("@Ordre", i + 1);
                cmd.Parameters.AddWithValue("@Description", echeanciers[i].Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@MontantTTC", echeanciers[i].MontantTTC);
                cmd.Parameters.AddWithValue("@DateEcheance", echeanciers[i].DateEcheance);
                cmd.Parameters.AddWithValue("@Statut", echeanciers[i].Statut);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<List<LigneFacture>> GetLignesFactureAsync(SqlConnection conn, int factureId)
        {
            var lignes = new List<LigneFacture>();

            using var cmd = new SqlCommand("SELECT * FROM LignesFactures WHERE FactureId = @FactureId ORDER BY Ordre", conn);
            cmd.Parameters.AddWithValue("@FactureId", factureId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lignes.Add(new LigneFacture
                {
                    Id = reader.GetInt32("Id"),
                    FactureId = reader.GetInt32("FactureId"),
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

        private async Task<List<Echeancier>> GetEcheanciersAsync(SqlConnection conn, int factureId)
        {
            var echeanciers = new List<Echeancier>();

            using var cmd = new SqlCommand("SELECT * FROM Echeanciers WHERE FactureId = @FactureId ORDER BY Ordre", conn);
            cmd.Parameters.AddWithValue("@FactureId", factureId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                echeanciers.Add(new Echeancier
                {
                    Id = reader.GetInt32("Id"),
                    FactureId = reader.GetInt32("FactureId"),
                    Ordre = reader.GetInt32("Ordre"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    MontantTTC = reader.GetDecimal("MontantTTC"),
                    DateEcheance = reader.GetDateTime("DateEcheance"),
                    Statut = reader.GetString("Statut"),
                    DatePaiement = reader.IsDBNull("DatePaiement") ? null : reader.GetDateTime("DatePaiement"),
                    ModePaiement = reader.IsDBNull("ModePaiement") ? null : reader.GetString("ModePaiement"),
                    ReferencePaiement = reader.IsDBNull("ReferencePaiement") ? null : reader.GetString("ReferencePaiement")
                });
            }

            return echeanciers;
        }

        private void AddFactureParameters(SqlCommand cmd, Facture facture)
        {
            cmd.Parameters.AddWithValue("@Numero", facture.Numero);
            cmd.Parameters.AddWithValue("@TypeFacture", facture.TypeFacture);
            cmd.Parameters.AddWithValue("@ClientId", facture.ClientId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SousTraitantId", facture.SousTraitantId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DevisId", facture.DevisId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ProjetId", facture.ProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Titre", facture.Titre);
            cmd.Parameters.AddWithValue("@Description", facture.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Statut", facture.Statut);
            cmd.Parameters.AddWithValue("@MontantHT", facture.MontantHT);
            cmd.Parameters.AddWithValue("@TauxTVA", facture.TauxTVA);
            cmd.Parameters.AddWithValue("@MontantTVA", facture.MontantTVA);
            cmd.Parameters.AddWithValue("@MontantTTC", facture.MontantTTC);
            cmd.Parameters.AddWithValue("@MontantPaye", facture.MontantPaye);
            cmd.Parameters.AddWithValue("@DateCreation", facture.DateCreation);
            cmd.Parameters.AddWithValue("@DateFacture", facture.DateFacture);
            cmd.Parameters.AddWithValue("@DateEcheance", facture.DateEcheance);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@ConditionsPaiement", facture.ConditionsPaiement);
            cmd.Parameters.AddWithValue("@ReferenceClient", facture.ReferenceClient ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", facture.UtilisateurCreation);
        }

        private Facture MapToFacture(SqlDataReader reader)
        {
            return new Facture
            {
                Id = reader.GetInt32("Id"),
                Numero = reader.GetString("Numero"),
                TypeFacture = reader.GetString("TypeFacture"),
                ClientId = reader.IsDBNull("ClientId") ? null : reader.GetInt32("ClientId"),
                SousTraitantId = reader.IsDBNull("SousTraitantId") ? null : reader.GetInt32("SousTraitantId"),
                DevisId = reader.IsDBNull("DevisId") ? null : reader.GetInt32("DevisId"),
                ProjetId = reader.IsDBNull("ProjetId") ? null : reader.GetInt32("ProjetId"),
                Titre = reader.GetString("Titre"),
                Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                Statut = reader.GetString("Statut"),
                MontantHT = reader.GetDecimal("MontantHT"),
                TauxTVA = reader.GetDecimal("TauxTVA"),
                MontantTVA = reader.GetDecimal("MontantTVA"),
                MontantTTC = reader.GetDecimal("MontantTTC"),
                MontantPaye = reader.GetDecimal("MontantPaye"),
                DateCreation = reader.GetDateTime("DateCreation"),
                DateFacture = reader.GetDateTime("DateFacture"),
                DateEcheance = reader.GetDateTime("DateEcheance"),
                DateEnvoi = reader.IsDBNull("DateEnvoi") ? null : reader.GetDateTime("DateEnvoi"),
                DatePaiement = reader.IsDBNull("DatePaiement") ? null : reader.GetDateTime("DatePaiement"),
                DateModification = reader.GetDateTime("DateModification"),
                ConditionsPaiement = reader.GetString("ConditionsPaiement"),
                ModePaiement = reader.IsDBNull("ModePaiement") ? null : reader.GetString("ModePaiement"),
                ReferenceClient = reader.IsDBNull("ReferenceClient") ? null : reader.GetString("ReferenceClient"),
                CheminPDF = reader.IsDBNull("CheminPDF") ? null : reader.GetString("CheminPDF"),
                UtilisateurCreation = reader.GetInt32("UtilisateurCreation")
            };
        }
    }
}
