// Controllers/Devis/DevisService.cs
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Clients;
using System.Collections.Generic;
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
        // HELPER CALCUL MONTANTS (inchangé)
        // =====================================================

        private (decimal montantHTBrut, decimal montantRemise, decimal montantHTNet, decimal montantTTC)
            CalculerMontantsAvecRemise(decimal montantHTBrut, decimal remiseValeur, decimal remisePourcentage, decimal tauxTVA)
        {
            decimal montantRemisePourcentage = montantHTBrut * (remisePourcentage / 100);
            decimal montantApresRemisePourcentage = montantHTBrut - montantRemisePourcentage;
            decimal montantHTNet = montantApresRemisePourcentage - remiseValeur;
            if (montantHTNet < 0) montantHTNet = 0;
            decimal montantRemiseTotal = montantHTBrut - montantHTNet;
            decimal montantTTC = montantHTNet * (1 + (tauxTVA / 100));
            return (montantHTBrut, montantRemiseTotal, montantHTNet, montantTTC);
        }

        // =====================================================
        // GET ALL (inchangé)
        // =====================================================

        public async Task<List<DevisListItem>> GetAllAsync()
        {
            var devisList = new List<DevisListItem>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT 
                    d.Id, d.Numero, d.Titre, d.Statut, d.MontantTTC, 
                    d.DateCreation, d.DateValidite, d.Chantier, d.UtilisateurCreation,
                    d.RemiseValeur, d.RemisePourcentage,
                    c.Id as ClientId, 
                    ISNULL(c.Nom, c.RaisonSociale) as ClientNom
                FROM Devis d
                LEFT JOIN Clients c ON d.ClientId = c.Id
                WHERE d.Actif = 1
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
                    RemiseValeur = reader.GetDecimal("RemiseValeur"),
                    RemisePourcentage = reader.GetDecimal("RemisePourcentage"),
                    DateCreation = reader.GetDateTime("DateCreation"),
                    DateValidite = reader.IsDBNull("DateValidite") ? null : reader.GetDateTime("DateValidite"),
                    Chantier = reader.IsDBNull("Chantier") ? null : reader.GetString("Chantier"),
                    Client = new ClientInfo
                    {
                        Id = reader.GetInt32("ClientId"),
                        Nom = reader.GetString("ClientNom")
                    },
                    UtilisateurCreation = reader.GetInt32("UtilisateurCreation")
                });
            }
            return devisList;
        }

        // =====================================================
        // GET BY ID — adapté pour charger les sous-sections
        // =====================================================

        public async Task<DevisCompletResponse?> GetByIdAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Résultats 1-3 via la procédure stockée existante
            using var cmd = new SqlCommand("sp_GetDevisComplet", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@DevisId", id);

            DevisCompletResponse? devisResponse = null;
            var sectionsDict = new Dictionary<int, DevisSectionResponse>();

            using var reader = await cmd.ExecuteReaderAsync();

            // Résultat 1 — En-tête devis
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
                    RemiseValeur = reader.GetDecimal("RemiseValeur"),
                    RemisePourcentage = reader.GetDecimal("RemisePourcentage"),
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
                    TypeDevis = reader.IsDBNull("TypeDevis") ? null : reader.GetString("TypeDevis"),
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
            if (devisResponse == null) return null;

            // Résultat 2 — Sections
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
                    SousSections = new List<DevisSousSectionResponse>(),
                    TotalSectionHT = 0
                };
                sectionsDict[section.Id] = section;
                devisResponse.Sections!.Add(section);
            }

            // Résultat 3 — Consommer le result set lignes de la proc (on le ignore,
            // on recharge les lignes en dessous avec SousSectionId + Code)
            await reader.NextResultAsync();
            while (await reader.ReadAsync()) { /* consommé */ }
            await reader.CloseAsync();

            // 🆕 Charger les sous-sections (s'il y en a — requête légère)
            var sousSectionsDict = new Dictionary<int, DevisSousSectionResponse>();
            using (var ssCmd = new SqlCommand(@"
                SELECT Id, SectionId, Code, Nom, Description, Ordre
                FROM DevisSousSections
                WHERE DevisId = @DevisId
                ORDER BY SectionId, Ordre", conn))
            {
                ssCmd.Parameters.AddWithValue("@DevisId", id);
                using var ssReader = await ssCmd.ExecuteReaderAsync();
                while (await ssReader.ReadAsync())
                {
                    var ss = new DevisSousSectionResponse
                    {
                        Id = ssReader.GetInt32("Id"),
                        SectionId = ssReader.GetInt32("SectionId"),
                        Code = ssReader.IsDBNull("Code") ? null : ssReader.GetString("Code"),
                        Nom = ssReader.GetString("Nom"),
                        Description = ssReader.IsDBNull("Description") ? null : ssReader.GetString("Description"),
                        Ordre = ssReader.GetInt32("Ordre"),
                        Lignes = new List<LigneDevisResponse>(),
                        TotalSousSectionHT = 0
                    };
                    sousSectionsDict[ss.Id] = ss;

                    if (sectionsDict.TryGetValue(ss.SectionId, out var parentSection))
                        parentSection.SousSections.Add(ss);
                }
            }

            // Recharger toutes les lignes avec SectionId + SousSectionId + Code
            using (var lignesCmd = new SqlCommand(@"
                SELECT Id, SectionId, SousSectionId, Ordre,
                       TypeElement, Designation, Description, Code,
                       Longueur, Hauteur, Quantite, Unite, PrixUnitaireHT,
                       Quantite * PrixUnitaireHT AS TotalHT
                FROM LignesDevis
                WHERE DevisId = @DevisId
                ORDER BY SectionId, ISNULL(SousSectionId, 0), Ordre", conn))
            {
                lignesCmd.Parameters.AddWithValue("@DevisId", id);
                using var lReader = await lignesCmd.ExecuteReaderAsync();

                while (await lReader.ReadAsync())
                {
                    var sectionId = lReader.IsDBNull("SectionId") ? 0 : lReader.GetInt32("SectionId");
                    var sousSectionId = lReader.IsDBNull("SousSectionId") ? (int?)null : lReader.GetInt32("SousSectionId");

                    var ligne = new LigneDevisResponse
                    {
                        Id = lReader.GetInt32("Id"),
                        Ordre = lReader.GetInt32("Ordre"),
                        TypeElement = lReader.IsDBNull("TypeElement") ? null : lReader.GetString("TypeElement"),
                        Designation = lReader.GetString("Designation"),
                        Description = lReader.IsDBNull("Description") ? null : lReader.GetString("Description"),
                        Code = lReader.IsDBNull("Code") ? null : lReader.GetString("Code"),
                        Longueur = lReader.IsDBNull("Longueur") ? null : lReader.GetDecimal("Longueur"),
                        Hauteur = lReader.IsDBNull("Hauteur") ? null : lReader.GetDecimal("Hauteur"),
                        Quantite = lReader.GetDecimal("Quantite"),
                        Unite = lReader.GetString("Unite"),
                        PrixUnitaireHT = lReader.GetDecimal("PrixUnitaireHT"),
                        TotalHT = lReader.GetDecimal("TotalHT"),
                        SousSectionId = sousSectionId,
                    };

                    if (sousSectionId.HasValue && sousSectionsDict.TryGetValue(sousSectionId.Value, out var ss))
                    {
                        // Ligne dans une sous-section
                        ss.Lignes.Add(ligne);
                        ss.TotalSousSectionHT += ligne.TotalHT;
                        if (sectionsDict.TryGetValue(ss.SectionId, out var parentSec))
                            parentSec.TotalSectionHT += ligne.TotalHT;
                    }
                    else if (sectionId > 0 && sectionsDict.TryGetValue(sectionId, out var section))
                    {
                        // Ligne directe sur la section
                        section.Lignes!.Add(ligne);
                        section.TotalSectionHT += ligne.TotalHT;
                    }
                }
            }

            return devisResponse;
        }

        // =====================================================
        // CREATE — adapté pour créer les sous-sections si présentes
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
                var numero = await GenerateNumeroWithTransactionAsync(conn, transaction);

                // Calculer montant HT brut (lignes directes + lignes de sous-sections)
                decimal montantHT = 0;
                if (request.Sections != null)
                {
                    foreach (var section in request.Sections)
                    {
                        if (section.Lignes != null)
                            montantHT += section.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);

                        if (section.SousSections != null)
                            foreach (var ss in section.SousSections)
                                if (ss.Lignes != null)
                                    montantHT += ss.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);
                    }
                }

                decimal tauxTVA = 18.00m;
                var (_, _, montantHTNet, montantTTC) = CalculerMontantsAvecRemise(
                    montantHT, request.RemiseValeur, request.RemisePourcentage, tauxTVA);

                using var devisCmd = new SqlCommand(@"
                    INSERT INTO Devis (
                        Numero, ClientId, Titre, Description, Statut,
                        MontantHT, TauxTVA, MontantTTC,
                        RemiseValeur, RemisePourcentage,
                        DateCreation, DateValidite, DateModification,
                        Conditions, Notes, UtilisateurCreation,
                        Chantier, Contact, QualiteMateriel, TypeVitrage,TypeDevis
                    )
                    VALUES (
                        @Numero, @ClientId, @Titre, @Description, @Statut,
                        @MontantHT, @TauxTVA, @MontantTTC,
                        @RemiseValeur, @RemisePourcentage,
                        @DateCreation, @DateValidite, @DateModification,
                        @Conditions, @Notes, @UtilisateurCreation,
                        @Chantier, @Contact, @QualiteMateriel, @TypeVitrage, @TypeDevis
                    );
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                devisCmd.Parameters.AddWithValue("@Numero", numero);
                devisCmd.Parameters.AddWithValue("@ClientId", request.ClientId);
                devisCmd.Parameters.AddWithValue("@Titre", request.Titre);
                devisCmd.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
                devisCmd.Parameters.AddWithValue("@Statut", "Brouillon");
                devisCmd.Parameters.AddWithValue("@MontantHT", montantHTNet);
                devisCmd.Parameters.AddWithValue("@TauxTVA", tauxTVA);
                devisCmd.Parameters.AddWithValue("@MontantTTC", montantTTC);
                devisCmd.Parameters.AddWithValue("@RemiseValeur", request.RemiseValeur);
                devisCmd.Parameters.AddWithValue("@RemisePourcentage", request.RemisePourcentage);
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
                devisCmd.Parameters.AddWithValue("@TypeDevis", request.TypeDevis ?? (object)DBNull.Value);

                var devisId = (int)await devisCmd.ExecuteScalarAsync();

                if (request.Sections != null && request.Sections.Any())
                    await CreateSectionsAsync(conn, transaction, devisId, request.Sections);

                using var updateMontantsCmd = new SqlCommand(@"
                    UPDATE Devis SET MontantHT = @MontantHT, MontantTTC = @MontantTTC, DateModification = GETDATE()
                    WHERE Id = @DevisId", conn, transaction);
                updateMontantsCmd.Parameters.AddWithValue("@DevisId", devisId);
                updateMontantsCmd.Parameters.AddWithValue("@MontantHT", montantHTNet);
                updateMontantsCmd.Parameters.AddWithValue("@MontantTTC", montantTTC);
                await updateMontantsCmd.ExecuteNonQueryAsync();

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
        // REMISE (inchangée)
        // =====================================================

        public async Task<bool> AppliquerRemiseAsync(int devisId, decimal? remiseValeur, decimal? remisePourcentage)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                using var getCmd = new SqlCommand(@"
                    SELECT MontantHT, TauxTVA, RemiseValeur, RemisePourcentage
                    FROM Devis WHERE Id = @Id", conn, transaction);
                getCmd.Parameters.AddWithValue("@Id", devisId);

                decimal montantHTActuel, tauxTVA, remiseValeurActuelle, remisePourcentageActuel;
                using (var reader = await getCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return false;
                    montantHTActuel = reader.GetDecimal(0);
                    tauxTVA = reader.GetDecimal(1);
                    remiseValeurActuelle = reader.GetDecimal(2);
                    remisePourcentageActuel = reader.GetDecimal(3);
                }

                decimal montantRemiseActuelle = remiseValeurActuelle + (montantHTActuel * remisePourcentageActuel / (100 - remisePourcentageActuel));
                decimal montantHTBrut = montantHTActuel + montantRemiseActuelle;

                decimal nouvelleRemiseValeur = remiseValeur ?? remiseValeurActuelle;
                decimal nouvelleRemisePourcentage = remisePourcentage ?? remisePourcentageActuel;

                var (_, _, montantHTNet, montantTTC) = CalculerMontantsAvecRemise(
                    montantHTBrut, nouvelleRemiseValeur, nouvelleRemisePourcentage, tauxTVA);

                using var updateCmd = new SqlCommand(@"
                    UPDATE Devis SET
                        RemiseValeur = @RemiseValeur, RemisePourcentage = @RemisePourcentage,
                        MontantHT = @MontantHT, MontantTTC = @MontantTTC, DateModification = @DateModification
                    WHERE Id = @Id", conn, transaction);
                updateCmd.Parameters.AddWithValue("@Id", devisId);
                updateCmd.Parameters.AddWithValue("@RemiseValeur", nouvelleRemiseValeur);
                updateCmd.Parameters.AddWithValue("@RemisePourcentage", nouvelleRemisePourcentage);
                updateCmd.Parameters.AddWithValue("@MontantHT", montantHTNet);
                updateCmd.Parameters.AddWithValue("@MontantTTC", montantTTC);
                updateCmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);
                await updateCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        // =====================================================
        // UPDATE — adapté pour gérer les sous-sections
        // =====================================================

        public async Task UpdateAsync(int devisId, CreateDevisRequest request)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                using var updateCmd = new SqlCommand(@"
                    UPDATE Devis SET
                        ClientId = @ClientId, Titre = @Titre, Description = @Description,
                        DateValidite = @DateValidite, DateModification = @DateModification,
                        Conditions = @Conditions, Notes = @Notes,
                        Chantier = @Chantier, Contact = @Contact,
                        QualiteMateriel = @QualiteMateriel, TypeVitrage = @TypeVitrage,
                        RemiseValeur = @RemiseValeur, RemisePourcentage = @RemisePourcentage
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
                updateCmd.Parameters.AddWithValue("@RemiseValeur", request.RemiseValeur);
                updateCmd.Parameters.AddWithValue("@RemisePourcentage", request.RemisePourcentage);
                await updateCmd.ExecuteNonQueryAsync();

                // Supprimer lignes → sous-sections → sections (respect des FK)
                using var deleteLignesCmd = new SqlCommand(
                    "DELETE FROM LignesDevis WHERE DevisId = @DevisId", conn, transaction);
                deleteLignesCmd.Parameters.AddWithValue("@DevisId", devisId);
                await deleteLignesCmd.ExecuteNonQueryAsync();

                // 🆕 Supprimer sous-sections avant les sections
                using var deleteSousSectionsCmd = new SqlCommand(
                    "DELETE FROM DevisSousSections WHERE DevisId = @DevisId", conn, transaction);
                deleteSousSectionsCmd.Parameters.AddWithValue("@DevisId", devisId);
                await deleteSousSectionsCmd.ExecuteNonQueryAsync();

                using var deleteSectionsCmd = new SqlCommand(
                    "DELETE FROM DevisSections WHERE DevisId = @DevisId", conn, transaction);
                deleteSectionsCmd.Parameters.AddWithValue("@DevisId", devisId);
                await deleteSectionsCmd.ExecuteNonQueryAsync();

                if (request.Sections != null && request.Sections.Any())
                    await CreateSectionsAsync(conn, transaction, devisId, request.Sections);

                // Recalculer les montants
                decimal montantHTBrut = 0;
                if (request.Sections != null)
                {
                    foreach (var section in request.Sections)
                    {
                        if (section.Lignes != null)
                            montantHTBrut += section.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);

                        if (section.SousSections != null)
                            foreach (var ss in section.SousSections)
                                if (ss.Lignes != null)
                                    montantHTBrut += ss.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);
                    }
                }

                decimal tauxTVA = 18.00m;
                var (_, _, montantHTNet, montantTTC) = CalculerMontantsAvecRemise(
                    montantHTBrut, request.RemiseValeur, request.RemisePourcentage, tauxTVA);

                using var updateMontantsCmd = new SqlCommand(@"
                    UPDATE Devis SET MontantHT = @MontantHT, MontantTTC = @MontantTTC, DateModification = GETDATE()
                    WHERE Id = @DevisId", conn, transaction);
                updateMontantsCmd.Parameters.AddWithValue("@DevisId", devisId);
                updateMontantsCmd.Parameters.AddWithValue("@MontantHT", montantHTNet);
                updateMontantsCmd.Parameters.AddWithValue("@MontantTTC", montantTTC);
                await updateMontantsCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        // =====================================================
        // DELETE (inchangé)
        // =====================================================

        public async Task DeleteAsync(int id)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                using var cmd = new SqlCommand(
                    "UPDATE Devis SET Actif = 0 WHERE Id = @Id", conn, transaction);
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
        }

        // =====================================================
        // STATUTS (inchangé)
        // =====================================================

        public async Task UpdateStatutAsync(int devisId, string nouveauStatut)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                UPDATE Devis SET
                    Statut         = @Statut,
                    DateEnvoi      = CASE WHEN @Statut = 'Envoye' THEN GETDATE() ELSE DateEnvoi END,
                    DateValidation = CASE WHEN @Statut = 'Valide' THEN GETDATE() ELSE DateValidation END,
                    DateModification = GETDATE()
                WHERE Id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", devisId);
            cmd.Parameters.AddWithValue("@Statut", nouveauStatut);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // =====================================================
        // RECHERCHE (inchangée)
        // =====================================================

        public async Task<RechercheDevisResult> RechercherAsync(RechercheDevisRequest request)
        {
            var result = new RechercheDevisResult { Page = request.Page, Devis = new List<DevisListItem>() };
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var whereConditions = new List<string> { "d.Actif = 1" };
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

            var whereClause = "WHERE " + string.Join(" AND ", whereConditions);

            using (var countCmd = new SqlCommand($@"
                SELECT COUNT(*) FROM Devis d LEFT JOIN Clients c ON d.ClientId = c.Id {whereClause}", conn))
            {
                countCmd.Parameters.AddRange(parameters.ToArray());
                result.Total = (int)await countCmd.ExecuteScalarAsync();
                result.TotalPages = (int)Math.Ceiling((double)result.Total / request.Limit);
            }

            var offset = (request.Page - 1) * request.Limit;
            using (var dataCmd = new SqlCommand($@"
                SELECT d.Id, d.Numero, d.Titre, d.Statut, d.MontantTTC,
                       d.DateCreation, d.DateValidite, d.Chantier,
                       c.Id as ClientId, ISNULL(c.RaisonSociale, c.Nom) as ClientNom
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
                        Client = new ClientInfo { Id = reader.GetInt32("ClientId"), Nom = reader.GetString("ClientNom") }
                    });
                }
            }
            return result;
        }

        // =====================================================
        // STATISTIQUES (inchangées)
        // =====================================================

        public async Task<StatistiquesDevis> GetStatistiquesAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(@"
                SELECT COUNT(*) AS Total,
                    SUM(CASE WHEN Statut='Brouillon'      THEN 1 ELSE 0 END) AS Brouillon,
                    SUM(CASE WHEN Statut='Envoye'         THEN 1 ELSE 0 END) AS Envoye,
                    SUM(CASE WHEN Statut='EnNegociation'  THEN 1 ELSE 0 END) AS EnNegociation,
                    SUM(CASE WHEN Statut='Valide'         THEN 1 ELSE 0 END) AS Valide,
                    SUM(CASE WHEN Statut='Refuse'         THEN 1 ELSE 0 END) AS Refuse,
                    SUM(CASE WHEN Statut='Expire'         THEN 1 ELSE 0 END) AS Expire,
                    ISNULL(SUM(MontantTTC), 0) AS MontantTotal,
                    ISNULL(SUM(CASE WHEN Statut='Valide' THEN MontantTTC ELSE 0 END), 0) AS MontantValide
                FROM Devis WHERE Actif = 1", conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new StatistiquesDevis
                {
                    Total = reader.IsDBNull("Total") ? 0 : reader.GetInt32("Total"),
                    Brouillon = reader.IsDBNull("Brouillon") ? 0 : reader.GetInt32("Brouillon"),
                    Envoye = reader.IsDBNull("Envoye") ? 0 : reader.GetInt32("Envoye"),
                    EnNegociation = reader.IsDBNull("EnNegociation") ? 0 : reader.GetInt32("EnNegociation"),
                    Valide = reader.IsDBNull("Valide") ? 0 : reader.GetInt32("Valide"),
                    Refuse = reader.IsDBNull("Refuse") ? 0 : reader.GetInt32("Refuse"),
                    Expire = reader.IsDBNull("Expire") ? 0 : reader.GetInt32("Expire"),
                    MontantTotal = reader.IsDBNull("MontantTotal") ? 0 : reader.GetDecimal("MontantTotal"),
                    MontantValide = reader.IsDBNull("MontantValide") ? 0 : reader.GetDecimal("MontantValide"),
                };
            }
            return new StatistiquesDevis();
        }

        // =====================================================
        // MÉTHODES PRIVÉES HELPERS
        // =====================================================

        private async Task<string> GenerateNumeroWithTransactionAsync(SqlConnection conn, SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year.ToString();
            using var cmd = new SqlCommand($@"
                SELECT ISNULL(MAX(CAST(RIGHT(Numero, 4) AS INT)), 0) + 1
                FROM Devis WHERE Numero LIKE 'DEV{annee}%'", conn, transaction);
            var prochainNumero = (int)await cmd.ExecuteScalarAsync();
            return $"DEV{annee}{prochainNumero:0000}";
        }

        /// <summary>
        /// Crée les sections avec leurs sous-sections et lignes.
        /// Les sous-sections sont facultatives (null ou liste vide = pas de sous-section).
        /// </summary>
        private async Task CreateSectionsAsync(
            SqlConnection conn, SqlTransaction transaction,
            int devisId, List<CreateDevisSectionRequest> sections)
        {
            foreach (var sectionRequest in sections)
            {
                using var sectionCmd = new SqlCommand(@"
                    INSERT INTO DevisSections (DevisId, Nom, Ordre, Description)
                    VALUES (@DevisId, @Nom, @Ordre, @Description);
                    SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                sectionCmd.Parameters.AddWithValue("@DevisId", devisId);
                sectionCmd.Parameters.AddWithValue("@Nom", sectionRequest.Nom);
                sectionCmd.Parameters.AddWithValue("@Ordre", sectionRequest.Ordre);
                sectionCmd.Parameters.AddWithValue("@Description", sectionRequest.Description ?? (object)DBNull.Value);

                var sectionId = (int)await sectionCmd.ExecuteScalarAsync();

                // 🆕 Sous-sections (facultatives)
                if (sectionRequest.SousSections != null && sectionRequest.SousSections.Any())
                {
                    foreach (var ssReq in sectionRequest.SousSections.OrderBy(s => s.Ordre))
                    {
                        using var ssCmd = new SqlCommand(@"
                            INSERT INTO DevisSousSections (SectionId, DevisId, Code, Nom, Description, Ordre, DateCreation)
                            VALUES (@SectionId, @DevisId, @Code, @Nom, @Description, @Ordre, @DateCreation);
                            SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

                        ssCmd.Parameters.AddWithValue("@SectionId", sectionId);
                        ssCmd.Parameters.AddWithValue("@DevisId", devisId);
                        ssCmd.Parameters.AddWithValue("@Code", ssReq.Code ?? (object)DBNull.Value);
                        ssCmd.Parameters.AddWithValue("@Nom", ssReq.Nom);
                        ssCmd.Parameters.AddWithValue("@Description", ssReq.Description ?? (object)DBNull.Value);
                        ssCmd.Parameters.AddWithValue("@Ordre", ssReq.Ordre);
                        ssCmd.Parameters.AddWithValue("@DateCreation", DateTime.UtcNow);

                        var sousSectionId = (int)await ssCmd.ExecuteScalarAsync();

                        if (ssReq.Lignes != null && ssReq.Lignes.Any())
                            await CreateLignesAsync(conn, transaction, devisId, sectionId, ssReq.Lignes, sousSectionId);
                    }
                }

                // Lignes directement sur la section (SousSectionId = NULL)
                if (sectionRequest.Lignes != null && sectionRequest.Lignes.Any())
                    await CreateLignesAsync(conn, transaction, devisId, sectionId, sectionRequest.Lignes, null);
            }
        }

        /// <summary>
        /// Insère les lignes d'une section ou d'une sous-section.
        /// sousSectionId = null → ligne directe sur la section.
        /// </summary>
        private async Task CreateLignesAsync(
            SqlConnection conn, SqlTransaction transaction,
            int devisId, int sectionId,
            List<CreateLigneDevisRequest> lignes,
            int? sousSectionId)
        {
            for (int i = 0; i < lignes.Count; i++)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO LignesDevis (
                        DevisId, SectionId, SousSectionId, Ordre,
                        TypeElement, Designation, Description, Code,
                        Longueur, Hauteur, Quantite, Unite, PrixUnitaireHT
                    )
                    VALUES (
                        @DevisId, @SectionId, @SousSectionId, @Ordre,
                        @TypeElement, @Designation, @Description, @Code,
                        @Longueur, @Hauteur, @Quantite, @Unite, @PrixUnitaireHT
                    )", conn, transaction);

                var ligne = lignes[i];
                cmd.Parameters.AddWithValue("@DevisId", devisId);
                cmd.Parameters.AddWithValue("@SectionId", sectionId);
                cmd.Parameters.AddWithValue("@SousSectionId", sousSectionId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ordre", i + 1);
                cmd.Parameters.AddWithValue("@TypeElement", ligne.TypeElement ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Designation", ligne.Designation);
                cmd.Parameters.AddWithValue("@Description", ligne.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Code", ligne.Code ?? (object)DBNull.Value);
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