// Controllers/Devis/DevisService.cs
using DocumentFormat.OpenXml.Spreadsheet;
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

        // =====================================================
        // MÉTHODES DE LECTURE
        // =====================================================

        public async Task<List<DevisListItem>> GetAllAsync()
        {
            var devisList = new List<DevisListItem>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    d.Id, d.Numero, d.Titre, d.Statut, d.MontantTTC, 
                    d.DateCreation, d.DateValidite, d.Chantier,
                    c.Id as ClientId, 
                    ISNULL(c.Nom, c.RaisonSociale ) as ClientNom
                FROM Devis d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                ORDER BY d.DateCreation DESC", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                devisList.Add(new DevisListItem
                {
                    Id = reader.GetInt32("Id"),
                    Numero = reader.GetString("Numero"),
                    Titre = reader.GetString("Titre"),
                    Statut = reader.GetString("Statut"),
                    MontantTTC = reader.GetDecimal("MontantTTC"),
                    DateCreation = reader.GetDateTime("DateCreation"),
                    DateValidite = reader.IsDBNull("DateValidite") ? null : reader.GetDateTime("DateValidite"),
                    Chantier = reader.IsDBNull("Chantier") ? null : reader.GetString("Chantier"),
                    Client = new ClientInfo
                    {
                        Id = reader.GetInt32("ClientId"),
                        Nom = reader.GetString("ClientNom")
                    }
                });
            }

            return devisList;
        }

        public async Task<DevisCompletResponse?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Utiliser la procédure stockée qui retourne tout d'un coup
            using var cmd = new SqlCommand("sp_GetDevisComplet", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@DevisId", id);

            DevisCompletResponse? devisResponse = null;
            Dictionary<int, DevisSectionResponse> sectionsDict = new();

            using var reader = await cmd.ExecuteReaderAsync();

            // Premier résultat: Informations du devis
            if (await reader.ReadAsync())
            {
                devisResponse = new DevisCompletResponse
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
                    Conditions = reader.IsDBNull("Conditions") ? null : reader.GetString("Conditions"),
                    Notes = reader.IsDBNull("Notes") ? null : reader.GetString("Notes"),
                    Chantier = reader.IsDBNull("Chantier") ? null : reader.GetString("Chantier"),
                    Contact = reader.IsDBNull("Contact") ? null : reader.GetString("Contact"),
                    QualiteMateriel = reader.IsDBNull("QualiteMateriel") ? null : reader.GetString("QualiteMateriel"),
                    TypeVitrage = reader.IsDBNull("TypeVitrage") ? null : reader.GetString("TypeVitrage"),
                    Client = new ClientInfo
                    {
                        Id = reader.GetInt32("ClientId"),
                        Nom = reader.IsDBNull("ClientNom") ? "" : reader.GetString("ClientNom"),
                        Email = reader.IsDBNull("ClientEmail") ? "" : reader.GetString("ClientEmail"),
                        Telephone = reader.IsDBNull("ClientTelephone") ? "" : reader.GetString("ClientTelephone"),
                        Adresse = reader.IsDBNull("ClientAdresse") ? "" : reader.GetString("ClientAdresse")
                    },
                    Sections = new List<DevisSectionResponse>()
                };
            }

            if (devisResponse == null)
                return null;

            // Deuxième résultat: Sections
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                var section = new DevisSectionResponse
                {
                    Id = reader.GetInt32("Id"),
                    Nom = reader.GetString("Nom"),
                    Ordre = reader.GetInt32("Ordre"),
                    Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                    Lignes = new List<LigneDevisResponse>(),
                    TotalSectionHT = 0
                };
                sectionsDict[section.Id] = section;
                devisResponse.Sections.Add(section);
            }

            // Troisième résultat: Lignes
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                var sectionId = reader.IsDBNull("SectionId") ? 0 : reader.GetInt32("SectionId");
                if (sectionId > 0 && sectionsDict.ContainsKey(sectionId))
                {
                    var ligne = new LigneDevisResponse
                    {
                        Id = reader.GetInt32("Id"),
                        Ordre = reader.GetInt32("Ordre"),
                        TypeElement = reader.IsDBNull("TypeElement") ? null : reader.GetString("TypeElement"),
                        Designation = reader.GetString("Designation"),
                        Description = reader.IsDBNull("Description") ? null : reader.GetString("Description"),
                        Longueur = reader.IsDBNull("Longueur") ? null : reader.GetDecimal("Longueur"),
                        Hauteur = reader.IsDBNull("Hauteur") ? null : reader.GetDecimal("Hauteur"),
                        Quantite = reader.GetDecimal("Quantite"),
                        Unite = reader.GetString("Unite"),
                        PrixUnitaireHT = reader.GetDecimal("PrixUnitaireHT"),
                        TotalHT = reader.GetDecimal("Quantite") * reader.GetDecimal("PrixUnitaireHT")
                    };

                    sectionsDict[sectionId].Lignes.Add(ligne);
                    sectionsDict[sectionId].TotalSectionHT += ligne.TotalHT;
                }
            }

            return devisResponse;
        }

        // =====================================================
        // MÉTHODES DE CRÉATION
        // =====================================================

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

        public async Task<int> CreateAsync(CreateDevisRequest request, int utilisateurId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Générer le numéro automatiquement
                var numero = await GenerateNumeroWithTransactionAsync(conn, transaction);

                // 2. Calculer les montants initiaux
                decimal montantHT = 0;
                if (request.Sections != null)
                {
                    foreach (var section in request.Sections)
                    {
                        if (section.Lignes != null)
                        {
                            montantHT += section.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);
                        }
                    }
                }

                decimal tauxTVA = 18.00m;
                decimal montantTTC = montantHT * (1 + tauxTVA / 100);

                // 3. Créer le devis
                using var devisCmd = new SqlCommand(@"
                    INSERT INTO Devis (
                        Numero, ClientId, Titre, Description, Statut, 
                        MontantHT, TauxTVA, MontantTTC,
                        DateCreation, DateValidite, DateModification, 
                        Conditions, Notes, UtilisateurCreation,
                        Chantier, Contact, QualiteMateriel, TypeVitrage
                    )
                    VALUES (
                        @Numero, @ClientId, @Titre, @Description, @Statut,
                        @MontantHT, @TauxTVA, @MontantTTC,
                        @DateCreation, @DateValidite, @DateModification,
                        @Conditions, @Notes, @UtilisateurCreation,
                        @Chantier, @Contact, @QualiteMateriel, @TypeVitrage
                    );
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                devisCmd.Parameters.AddWithValue("@Numero", numero);
                devisCmd.Parameters.AddWithValue("@ClientId", request.ClientId);
                devisCmd.Parameters.AddWithValue("@Titre", request.Titre);
                devisCmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@Statut", "Brouillon");
                devisCmd.Parameters.AddWithValue("@MontantHT", montantHT);
                devisCmd.Parameters.AddWithValue("@TauxTVA", tauxTVA);
                devisCmd.Parameters.AddWithValue("@MontantTTC", montantTTC);
                devisCmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);
                devisCmd.Parameters.AddWithValue("@DateValidite", request.DateValidite ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
                devisCmd.Parameters.AddWithValue("@Conditions", request.Conditions ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@Notes", request.Notes ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@UtilisateurCreation", utilisateurId);
                devisCmd.Parameters.AddWithValue("@Chantier", request.Chantier ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@Contact", request.Contact ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@QualiteMateriel", request.QualiteMateriel ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@TypeVitrage", request.TypeVitrage ?? (object)DBNull.Value);

                var devisId = (int)await devisCmd.ExecuteScalarAsync();

                // 4. Créer les sections et leurs lignes
                if (request.Sections != null && request.Sections.Any())
                {
                    await CreateSectionsAsync(conn, transaction, devisId, request.Sections);
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

        // =====================================================
        // MÉTHODES DE MISE À JOUR
        // =====================================================

        public async Task UpdateAsync(int devisId, CreateDevisRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Mettre à jour le devis principal
                using var updateCmd = new SqlCommand(@"
                    UPDATE Devis SET 
                        ClientId = @ClientId, 
                        Titre = @Titre, 
                        Description = @Description,
                        DateValidite = @DateValidite, 
                        DateModification = @DateModification, 
                        Conditions = @Conditions, 
                        Notes = @Notes,
                        Chantier = @Chantier,
                        Contact = @Contact,
                        QualiteMateriel = @QualiteMateriel,
                        TypeVitrage = @TypeVitrage
                    WHERE Id = @Id", conn, transaction);

                updateCmd.Parameters.AddWithValue("@Id", devisId);
                updateCmd.Parameters.AddWithValue("@ClientId", request.ClientId);
                updateCmd.Parameters.AddWithValue("@Titre", request.Titre);
                updateCmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@DateValidite", request.DateValidite ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
                updateCmd.Parameters.AddWithValue("@Conditions", request.Conditions ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@Notes", request.Notes ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@Chantier", request.Chantier ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@Contact", request.Contact ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@QualiteMateriel", request.QualiteMateriel ?? (object)DBNull.Value);
                updateCmd.Parameters.AddWithValue("@TypeVitrage", request.TypeVitrage ?? (object)DBNull.Value);

                await updateCmd.ExecuteNonQueryAsync();

                // 2. Supprimer toutes les sections existantes (cascade supprime aussi les lignes)
                using var deleteSectionsCmd = new SqlCommand(
                    "DELETE FROM DevisSections WHERE DevisId = @DevisId", conn, transaction);
                deleteSectionsCmd.Parameters.AddWithValue("@DevisId", devisId);
                await deleteSectionsCmd.ExecuteNonQueryAsync();

                // 3. Recréer les sections et lignes
                if (request.Sections != null && request.Sections.Any())
                {
                    await CreateSectionsAsync(conn, transaction, devisId, request.Sections);
                }

                // 4. Le trigger trg_UpdateDevisMontants calculera automatiquement les nouveaux montants

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =====================================================
        // MÉTHODES DE SUPPRESSION
        // =====================================================

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Les contraintes CASCADE supprimeront automatiquement les sections et lignes
                using var deleteCmd = new SqlCommand("DELETE FROM Devis WHERE Id = @Id", conn, transaction);
                deleteCmd.Parameters.AddWithValue("@Id", id);
                await deleteCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =====================================================
        // GESTION DES STATUTS
        // =====================================================

        public async Task UpdateStatutAsync(int devisId, string nouveauStatut)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Devis 
                SET Statut = @Statut,
                    DateEnvoi = CASE WHEN @Statut = 'Envoye' THEN GETDATE() ELSE DateEnvoi END,
                    DateValidation = CASE WHEN @Statut = 'Valide' THEN GETDATE() ELSE DateValidation END,
                    DateModification = GETDATE()
                WHERE Id = @Id", conn);

            cmd.Parameters.AddWithValue("@Id", devisId);
            cmd.Parameters.AddWithValue("@Statut", nouveauStatut);

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // =====================================================
        // RECHERCHE ET STATISTIQUES
        // =====================================================

        public async Task<RechercheDevisResult> RechercherAsync(RechercheDevisRequest request)
        {
            var result = new RechercheDevisResult
            {
                Page = request.Page,
                Devis = new List<DevisListItem>()
            };

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Construction de la requête dynamique
            var whereConditions = new List<string>();
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                whereConditions.Add("(d.Numero LIKE @Search OR d.Titre LIKE @Search OR c.Nom LIKE @Search OR c.RaisonSociale LIKE @Search)");
                parameters.Add(new SqlParameter("@Search", $"%{request.Search}%"));
            }

            if (!string.IsNullOrWhiteSpace(request.Statut))
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

            var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

            // Compter le total
            using (var countCmd = new SqlCommand($@"
                SELECT COUNT(*) 
                FROM Devis d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                {whereClause}", conn))
            {
                countCmd.Parameters.AddRange(parameters.ToArray());
                result.Total = (int)await countCmd.ExecuteScalarAsync();
                result.TotalPages = (int)Math.Ceiling((double)result.Total / request.Limit);
            }

            // Récupérer les données paginées
            var offset = (request.Page - 1) * request.Limit;
            using (var dataCmd = new SqlCommand($@"
                SELECT 
                    d.Id, d.Numero, d.Titre, d.Statut, d.MontantTTC, 
                    d.DateCreation, d.DateValidite, d.Chantier,
                    c.Id as ClientId, 
                    ISNULL(c.RaisonSociale, c.Nom) as ClientNom
                FROM Devis d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                {whereClause}
                ORDER BY d.DateCreation DESC
                OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY", conn))
            {
                dataCmd.Parameters.AddRange(parameters.ToArray());
                dataCmd.Parameters.AddWithValue("@Offset", offset);
                dataCmd.Parameters.AddWithValue("@Limit", request.Limit);

                using var reader = await dataCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Devis.Add(new DevisListItem
                    {
                        Id = reader.GetInt32("Id"),
                        Numero = reader.GetString("Numero"),
                        Titre = reader.GetString("Titre"),
                        Statut = reader.GetString("Statut"),
                        MontantTTC = reader.GetDecimal("MontantTTC"),
                        DateCreation = reader.GetDateTime("DateCreation"),
                        DateValidite = reader.IsDBNull("DateValidite") ? null : reader.GetDateTime("DateValidite"),
                        Chantier = reader.IsDBNull("Chantier") ? null : reader.GetString("Chantier"),
                        Client = new ClientInfo
                        {
                            Id = reader.GetInt32("ClientId"),
                            Nom = reader.GetString("ClientNom")
                        }
                    });
                }
            }

            return result;
        }

        public async Task<StatistiquesDevis> GetStatistiquesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                   SELECT 
                   COUNT(*) AS Total,
                    SUM(CASE WHEN Statut = 'Brouillon' THEN 1 ELSE 0 END) AS Brouillon,
                    SUM(CASE WHEN Statut = 'Envoye' THEN 1 ELSE 0 END) AS Envoye,
                    SUM(CASE WHEN Statut = 'EnNegociation' THEN 1 ELSE 0 END) AS EnNegociation,
                    SUM(CASE WHEN Statut = 'Valide' THEN 1 ELSE 0 END) AS Valide,
                    SUM(CASE WHEN Statut = 'Refuse' THEN 1 ELSE 0 END) AS Refuse,
                    SUM(CASE WHEN Statut = 'Expire' THEN 1 ELSE 0 END) AS Expire,
                    ISNULL(SUM(MontantTTC), 0) AS MontantTotal,
                    ISNULL(SUM(CASE WHEN Statut = 'Valide' THEN MontantTTC ELSE 0 END), 0) AS MontantValide
                    FROM  Devis", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new StatistiquesDevis
                {
                    Total = reader.IsDBNull("Total")
                    ? 0
                    : reader.GetInt32("Total"),
                    Brouillon = reader.IsDBNull("Brouillon")
                    ? 0
                    : reader.GetInt32("Brouillon"),
                    Envoye = reader.IsDBNull("Envoye")
                    ? 0
                    : reader.GetInt32("Envoye"),
                    EnNegociation = reader.IsDBNull("EnNegociation")
                    ? 0
                    : reader.GetInt32("EnNegociation"),
                    Valide = reader.IsDBNull("Valide")
                    ? 0
                    : reader.GetInt32("Valide"),
                    Refuse = reader.IsDBNull("Refuse")
                    ? 0
                    : reader.GetInt32("Refuse"),
                    Expire = reader.IsDBNull("Expire")
                    ? 0
                    : reader.GetInt32("Expire"),
                    MontantTotal = reader.IsDBNull("MontantTotal")
                    ? 0
                    : reader.GetDecimal("MontantTotal"),

                    MontantValide = reader.IsDBNull("MontantValide")
                    ? 0
                    : reader.GetDecimal("MontantValide"),
                };
            }

            return new StatistiquesDevis();
        }

        // =====================================================
        // GÉNÉRATION PDF
        // =====================================================

        public async Task<byte[]> GeneratePDFAsync(int devisId)
        {
            var devis = await GetByIdAsync(devisId);
            if (devis == null)
                throw new Exception("Devis introuvable");

            var pdfService = new DevisPDFService();
            return await Task.Run(() => pdfService.GeneratePDF(devis));
        }

        // =====================================================
        // MÉTHODES PRIVÉES HELPERS
        // =====================================================

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

        private async Task CreateSectionsAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int devisId,
            List<CreateDevisSectionRequest> sections)
        {
            foreach (var sectionRequest in sections)
            {
                // Créer la section
                using var sectionCmd = new SqlCommand(@"
                    INSERT INTO DevisSections (DevisId, Nom, Ordre, Description)
                    VALUES (@DevisId, @Nom, @Ordre, @Description);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                sectionCmd.Parameters.AddWithValue("@DevisId", devisId);
                sectionCmd.Parameters.AddWithValue("@Nom", sectionRequest.Nom);
                sectionCmd.Parameters.AddWithValue("@Ordre", sectionRequest.Ordre);
                sectionCmd.Parameters.AddWithValue("@Description", sectionRequest.Description ?? (object)DBNull.Value);

                var sectionId = (int)await sectionCmd.ExecuteScalarAsync();

                // Créer les lignes de la section
                if (sectionRequest.Lignes != null && sectionRequest.Lignes.Any())
                {
                    await CreateLignesAsync(conn, transaction, devisId, sectionId, sectionRequest.Lignes);
                }
            }
        }

        private async Task CreateLignesAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int devisId,
            int sectionId,
            List<CreateLigneDevisRequest> lignes)
        {
            for (int i = 0; i < lignes.Count; i++)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO LignesDevis (
                        DevisId, SectionId, Ordre, TypeElement, Designation, Description, 
                        Longueur, Hauteur, Quantite, Unite, PrixUnitaireHT
                    )
                    VALUES (
                        @DevisId, @SectionId, @Ordre, @TypeElement, @Designation, @Description,
                        @Longueur, @Hauteur, @Quantite, @Unite, @PrixUnitaireHT
                    )", conn, transaction);

                var ligne = lignes[i];
                cmd.Parameters.AddWithValue("@DevisId", devisId);
                cmd.Parameters.AddWithValue("@SectionId", sectionId);
                cmd.Parameters.AddWithValue("@Ordre", i + 1);
                cmd.Parameters.AddWithValue("@TypeElement", ligne.TypeElement ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Designation", ligne.Designation);
                cmd.Parameters.AddWithValue("@Description", ligne.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Longueur", ligne.Longueur ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Hauteur", ligne.Hauteur ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Quantite", ligne.Quantite);
                cmd.Parameters.AddWithValue("@Unite", ligne.Unite);
                cmd.Parameters.AddWithValue("@PrixUnitaireHT", ligne.PrixUnitaireHT);

                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}