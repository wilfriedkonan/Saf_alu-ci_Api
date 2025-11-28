using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using Saf_alu_ci_Api.Controllers.Dqe;
using Saf_alu_ci_Api.Controllers.Projets;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    /// <summary>
    /// 🆕 Service de conversion DQE → Projet avec hiérarchie Étapes → Sous-étapes
    /// Lots DQE → Étapes principales (Niveau 1)
    /// Items DQE → Sous-étapes (Niveau 2)
    /// </summary>
    public class ConversionService
    {
        private readonly string _connectionString;
        private readonly DQEService _dqeService;
        private readonly ProjetService _projetService;

        public ConversionService(
            string connectionString,
            DQEService dqeService,
            ProjetService projetService)
        {
            _connectionString = connectionString;
            _dqeService = dqeService;
            _projetService = projetService;
        }

        // ========================================
        // MÉTHODE PRINCIPALE DE CONVERSION
        // ========================================

        /// <summary>
        /// Convertit un DQE en projet avec hiérarchie complète :
        /// - Lots → Étapes principales
        /// - Items → Sous-étapes
        /// </summary>
        public async Task<int> ConvertDQEToProjectAsync(
            int dqeId,
            ConvertDQEToProjectRequest request,
            int utilisateurId)
        {
            // 1. Vérifier que le DQE peut être converti
            var (canConvert, reason) = await _dqeService.CanConvertToProjectAsync(dqeId);
            if (!canConvert)
            {
                throw new InvalidOperationException(reason);
            }

            // 2. Récupérer le DQE complet
            var dqe = await _dqeService.GetByIdAsync(dqeId);
            if (dqe == null)
            {
                throw new InvalidOperationException("DQE introuvable");
            }

            // 3. Créer le projet avec transaction
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 3a. Créer le projet
                var projet = BuildProjectFromDQE(dqe, request, utilisateurId);
                var projetId = await CreateProjectWithTransactionAsync(conn, transaction, projet);

                // 3b. Créer les étapes principales (Lots) et sous-étapes (Items)
                if (request.ModeCreationEtapes == "automatique")
                {
                    await CreateHierarchicalStagesAsync(
                        conn,
                        transaction,
                        projetId,
                        dqe.Lots,
                        request.DateDebut,
                        request.DureeTotaleJours,
                        request.MethodeCalculDurees,
                        dqe.Reference
                    );
                }

                // 3c. Marquer le DQE comme converti
                await MarkDQEAsConvertedAsync(
                    conn,
                    transaction,
                    dqeId,
                    projetId,
                    projet.Numero,
                    utilisateurId
                );

                transaction.Commit();
                return projetId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        // ========================================
        // 🆕 CRÉATION HIÉRARCHIQUE DES ÉTAPES
        // ========================================

        /// <summary>
        /// Crée les étapes principales (Lots) et leurs sous-étapes (Items)
        /// </summary>
        private async Task CreateHierarchicalStagesAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            List<DQELotDTO> lots,
            DateTime dateDebut,
            int dureeTotaleJours,
            string methodeCalculDurees,
            string dqeReference)
        {
            if (lots == null || lots.Count == 0)
                return;

            int ordreEtapePrincipale = 1;
            //ici Niveau correspond a etaps 
            var Niveau = 0;
            foreach (var lot in lots.OrderBy(l => l.Ordre))
            {
                Niveau++;
                // ================================================
                // ÉTAPE 1 : Créer l'étape principale (Lot)
                // ================================================

                var dureeEtape = CalculerDureeEtape(
                    lot,
                    lots,
                    dureeTotaleJours,
                    methodeCalculDurees
                );

                var dateDebutEtape = ordreEtapePrincipale == 1
                    ? dateDebut
                    : dateDebut.AddDays(CalculerOffsetDate(ordreEtapePrincipale - 1, lots, dureeTotaleJours, methodeCalculDurees));

                var dateFinEtape = dateDebutEtape.AddDays(dureeEtape);

                // Budget de l'étape = somme des items
                var budgetEtape = lot.Chapters
                    .SelectMany(c => c.Items)
                    .Sum(i => i.TotalRevenueHT);

                var CoutReelEtape = lot.Chapters
                    .SelectMany(c => c.Items)
                    .Sum(i => i.TotalRevenueHT);

                // Insérer l'étape principale
                var etapeId = await InsertEtapePrincipaleAsync(
                    conn,
                    transaction,
                    projetId,
                    lot,
                    ordreEtapePrincipale,
                    dateDebutEtape,
                    dateFinEtape,
                    budgetEtape,
                    CoutReelEtape,
                    dqeReference,
                    Niveau
                );

                // ================================================
                // ÉTAPE 2 : Créer les sous-étapes (Items)
                // ================================================

                int ordreSousEtape = 1;

                foreach (var chapter in lot.Chapters.OrderBy(c => c.Ordre))
                {
                    foreach (var item in chapter.Items.OrderBy(i => i.Ordre))
                    {
                        await InsertSousEtapeAsync(
                            conn,
                            transaction,
                            projetId,
                            etapeId, // Parent = étape principale
                            item,
                            chapter,
                            lot,
                            ordreSousEtape,
                            dateDebutEtape,
                            dateFinEtape,
                            dqeReference,
                            Niveau
                        );

                        ordreSousEtape++;
                    }
                }

                ordreEtapePrincipale++;
            }
        }

        /// <summary>
        /// 🆕 Insère une étape principale (Lot DQE)
        /// </summary>
        private async Task<int> InsertEtapePrincipaleAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            DQELotDTO lot,
            int ordre,
            DateTime dateDebut,
            DateTime dateFinPrevue,
            decimal budgetPrevu,
            decimal CoutReelEtape,
            string dqeReference,
            int Niveau)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO EtapesProjets (
                    ProjetId, Nom, Description, Ordre,
                    EtapeParentId, Niveau, TypeEtape,
                    DateDebut, DateFinPrevue,
                    Statut, PourcentageAvancement,
                    BudgetPrevu, CoutReel, Depense,
                    TypeResponsable,
                    LinkedDqeLotId, LinkedDqeLotCode, LinkedDqeLotName, LinkedDqeReference,
                    EstActif, DateCreation, DateModification
                ) VALUES (
                    @ProjetId, @Nom, @Description, @Ordre,
                    NULL, @Niveau, 'Lot',
                    @DateDebut, @DateFinPrevue,
                    'NonCommence', 0,
                    @BudgetPrevu, @CoutReel, 0,
                    'Interne',
                    @LinkedDqeLotId, @LinkedDqeLotCode, @LinkedDqeLotName, @LinkedDqeReference,
                    1, GETUTCDATE(), GETUTCDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@Nom", lot.Nom);
            cmd.Parameters.AddWithValue("@Description", lot.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", ordre);
            cmd.Parameters.AddWithValue("@Niveau", Niveau);
            cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
            cmd.Parameters.AddWithValue("@DateFinPrevue", dateFinPrevue);
            cmd.Parameters.AddWithValue("@BudgetPrevu", budgetPrevu);
            cmd.Parameters.AddWithValue("@CoutReel", CoutReelEtape);
            cmd.Parameters.AddWithValue("@LinkedDqeLotId", lot.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeLotCode", lot.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeLotName", lot.Nom);
            cmd.Parameters.AddWithValue("@LinkedDqeReference", dqeReference);

            var etapeId = (int)await cmd.ExecuteScalarAsync();
            return etapeId;
        }

        /// <summary>
        /// 🆕 Insère une sous-étape (Item DQE)
        /// </summary>
        private async Task InsertSousEtapeAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            int etapeParentId,
            DQEItemDTO item,
            DQEChapterDTO chapter,
            DQELotDTO lot,
            int ordre,
            DateTime dateDebut,
            DateTime dateFinPrevue,
            string dqeReference,
            int Niveau)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO EtapesProjets (
                    ProjetId, Nom, Description, Ordre,
                    EtapeParentId, Niveau, TypeEtape,
                    DateDebut, DateFinPrevue,
                    Statut, PourcentageAvancement,
                    BudgetPrevu, CoutReel, Depense,
                    Unite, QuantitePrevue, QuantiteRealisee, PrixUnitairePrevu,
                    TypeResponsable,
                    LinkedDqeLotId, LinkedDqeLotCode,
                    LinkedDqeChapterId, LinkedDqeChapterCode,
                    LinkedDqeItemId, LinkedDqeItemCode,
                    LinkedDqeReference,
                    EstActif, DateCreation, DateModification
                ) VALUES (
                    @ProjetId, @Nom, @Description, @Ordre,
                    @EtapeParentId, @Niveau, 'Item',
                    @DateDebut, @DateFinPrevue,
                    'NonCommence', 0,
                    @BudgetPrevu, @CoutReel, 0,
                    @Unite, @QuantitePrevue, 0, @PrixUnitairePrevu,
                    'Interne',
                    @LinkedDqeLotId, @LinkedDqeLotCode,
                    @LinkedDqeChapterId, @LinkedDqeChapterCode,
                    @LinkedDqeItemId, @LinkedDqeItemCode,
                    @LinkedDqeReference,
                    1, GETUTCDATE(), GETUTCDATE()
                );", conn, transaction);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@Nom", item.Designation);
            cmd.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", ordre);
            cmd.Parameters.AddWithValue("@EtapeParentId", etapeParentId);
            cmd.Parameters.AddWithValue("@Niveau", Niveau);
            cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
            cmd.Parameters.AddWithValue("@DateFinPrevue", dateFinPrevue);
            cmd.Parameters.AddWithValue("@BudgetPrevu", item.TotalRevenueHT);
            cmd.Parameters.AddWithValue("@CoutReel", item.DeboursseSec);
            cmd.Parameters.AddWithValue("@Unite", item.Unite);
            cmd.Parameters.AddWithValue("@QuantitePrevue", item.Quantite);
            cmd.Parameters.AddWithValue("@PrixUnitairePrevu", item.PrixUnitaireHT);
            cmd.Parameters.AddWithValue("@LinkedDqeLotId", lot.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeLotCode", lot.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeChapterId", chapter.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeChapterCode", chapter.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeItemId", item.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeItemCode", item.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeReference", dqeReference);

            await cmd.ExecuteNonQueryAsync();
        }

        // ========================================
        // MÉTHODES UTILITAIRES (inchangées)
        // ========================================

        private Projet BuildProjectFromDQE(
            DQEDetailDTO dqe,
            ConvertDQEToProjectRequest request,
            int utilisateurId)
        {
            var nomProjet = string.IsNullOrEmpty(request.NomProjet) ? dqe.Nom : request.NomProjet;
            var descriptionProjet = string.IsNullOrEmpty(request.DescriptionProjet) ?
                dqe.Description : request.DescriptionProjet;

            var dateFinPrevue = request.DateDebut.AddDays(request.DureeTotaleJours);

            return new Projet
            {
                Nom = nomProjet,
                Description = descriptionProjet,
                ClientId = dqe.Client.Id,
                TypeProjetId = request.TypeProjetId,
                Statut = request.StatutInitial,
                DateDebut = request.DateDebut,
                DateFinPrevue = dateFinPrevue,
                BudgetInitial = dqe.TotalRevenueHT,
                BudgetRevise = dqe.TotalRevenueHT,
                CoutReel = 0,
                ChefProjetId = request.ChefProjetId,
                PourcentageAvancement = 0,
                DateCreation = DateTime.UtcNow,
                DateModification = DateTime.UtcNow,
                UtilisateurCreation = utilisateurId,
                Actif = true,
                IsFromDqeConversion = true,
                LinkedDqeId = dqe.Id,
                LinkedDqeReference = dqe.Reference,
                LinkedDqeName = dqe.Nom,
                LinkedDqeBudgetHT = dqe.TotalRevenueHT,
                DqeConvertedAt = DateTime.UtcNow,
                DqeConvertedById = utilisateurId
            };
        }

        private async Task<int> CreateProjectWithTransactionAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            Projet projet)
        {
            projet.Numero = await GenerateProjectNumberWithTransactionAsync(conn, transaction);

            using var cmd = new SqlCommand(@"
                INSERT INTO Projets (
                    Numero, Nom, Description, ClientId, TypeProjetId,
                    Statut, DateDebut, DateFinPrevue,
                    BudgetInitial, BudgetRevise, CoutReel,
                    PourcentageAvancement, ChefProjetId,
                    DateCreation, DateModification, UtilisateurCreation, Actif,
                    IsFromDqeConversion, LinkedDqeId, LinkedDqeReference,
                    LinkedDqeName, LinkedDqeBudgetHT,
                    DqeConvertedAt, DqeConvertedById
                ) VALUES (
                    @Numero, @Nom, @Description, @ClientId, @TypeProjetId,
                    @Statut, @DateDebut, @DateFinPrevue,
                    @BudgetInitial, @BudgetRevise, @CoutReel,
                    @PourcentageAvancement, @ChefProjetId,
                    @DateCreation, @DateModification, @UtilisateurCreation, @Actif,
                    @IsFromDqeConversion, @LinkedDqeId, @LinkedDqeReference,
                    @LinkedDqeName, @LinkedDqeBudgetHT,
                    @DqeConvertedAt, @DqeConvertedById
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction);

            cmd.Parameters.AddWithValue("@Numero", projet.Numero);
            cmd.Parameters.AddWithValue("@Nom", projet.Nom);
            cmd.Parameters.AddWithValue("@Description", projet.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientId", projet.ClientId);
            cmd.Parameters.AddWithValue("@TypeProjetId", projet.TypeProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Statut", projet.Statut);
            cmd.Parameters.AddWithValue("@DateDebut", projet.DateDebut ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateFinPrevue", projet.DateFinPrevue ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BudgetInitial", projet.BudgetInitial);
            cmd.Parameters.AddWithValue("@BudgetRevise", projet.BudgetRevise);
            cmd.Parameters.AddWithValue("@CoutReel", projet.CoutReel);
            cmd.Parameters.AddWithValue("@PourcentageAvancement", projet.PourcentageAvancement);
            cmd.Parameters.AddWithValue("@ChefProjetId", projet.ChefProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateCreation", projet.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", projet.DateModification);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", projet.UtilisateurCreation);
            cmd.Parameters.AddWithValue("@Actif", projet.Actif);
            cmd.Parameters.AddWithValue("@IsFromDqeConversion", projet.IsFromDqeConversion);
            cmd.Parameters.AddWithValue("@LinkedDqeId", projet.LinkedDqeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeReference", projet.LinkedDqeReference ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeName", projet.LinkedDqeName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeBudgetHT", projet.LinkedDqeBudgetHT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DqeConvertedAt", projet.DqeConvertedAt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DqeConvertedById", projet.DqeConvertedById ?? (object)DBNull.Value);

            var projetId = (int)await cmd.ExecuteScalarAsync();
            return projetId;
        }

        private async Task MarkDQEAsConvertedAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int dqeId,
            int projetId,
            string projetNumero,
            int utilisateurId)
        {
            using var cmd = new SqlCommand(@"
                UPDATE DQE SET
                    IsConverted = 1,
                    LinkedProjectId = @LinkedProjectId,
                    LinkedProjectNumber = @LinkedProjectNumber,
                    ConvertedAt = @ConvertedAt,
                    ConvertedById = @ConvertedById,
                    DateModification = @DateModification
                WHERE Id = @Id", conn, transaction);

            cmd.Parameters.AddWithValue("@Id", dqeId);
            cmd.Parameters.AddWithValue("@IsConverted", true);
            cmd.Parameters.AddWithValue("@LinkedProjectId", projetId);
            cmd.Parameters.AddWithValue("@LinkedProjectNumber", projetNumero);
            cmd.Parameters.AddWithValue("@ConvertedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@ConvertedById", utilisateurId);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<string> GenerateProjectNumberWithTransactionAsync(
            SqlConnection conn,
            SqlTransaction transaction)
        {
            var annee = DateTime.UtcNow.Year.ToString();

            using var cmd = new SqlCommand($@"
                SELECT ISNULL(MAX(CAST(RIGHT(Numero, 4) AS INT)), 0) + 1
                FROM Projets 
                WHERE Numero LIKE 'PRJ{annee}%'", conn, transaction);

            var prochainNumero = (int)await cmd.ExecuteScalarAsync();
            return $"PRJ{annee}{prochainNumero:0000}";
        }

        private int CalculerDureeEtape(
            DQELotDTO lot,
            List<DQELotDTO> tousLesLots,
            int dureeTotale,
            string methode)
        {
            if (methode == "proportionnel")
            {
                var totalBudget = tousLesLots.Sum(l => l.TotalRevenueHT);
                if (totalBudget == 0) return dureeTotale / tousLesLots.Count;

                var proportion = lot.TotalRevenueHT / totalBudget;
                return Math.Max(1, (int)(dureeTotale * proportion));
            }
            else // équitable
            {
                return dureeTotale / tousLesLots.Count;
            }
        }

        private int CalculerOffsetDate(
            int index,
            List<DQELotDTO> lots,
            int dureeTotale,
            string methode)
        {
            int offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset += CalculerDureeEtape(lots[i], lots, dureeTotale, methode);
            }
            return offset;
        }

        // Prévisualisation (simplifiée pour l'exemple)
        public async Task<ConversionPreviewDTO> PreviewConversionAsync(
            int dqeId,
            ConvertDQEToProjectRequest request)
        {
            var dqe = await _dqeService.GetByIdAsync(dqeId);
            if (dqe == null)
            {
                throw new InvalidOperationException("DQE introuvable");
            }

            var numeroProjet = await _projetService.GenerateNumeroAsync();
            var dateFinPrevue = request.DateDebut.AddDays(request.DureeTotaleJours);

            var projectPreview = new ProjectPreviewDTO
            {
                Nom = string.IsNullOrEmpty(request.NomProjet) ? dqe.Nom : request.NomProjet,
                NumeroProjet = numeroProjet,
                BudgetInitial = dqe.TotalRevenueHT,
                DateDebut = request.DateDebut,
                DateFinPrevue = dateFinPrevue,
                DureeTotaleJours = request.DureeTotaleJours
            };

            var stagesPreview = new List<StagePreviewDTO>();
            var Niveau = 0;
            // Ajouter les étapes principales avec leurs sous-étapes
            foreach (var lot in dqe.Lots.OrderBy(l => l.Ordre))
            { //ici niveau egale lot 
                Niveau += 1;
                var nombreSousEtapes = lot.Chapters.SelectMany(c => c.Items).Count();

                stagesPreview.Add(new StagePreviewDTO
                {
                    Nom = lot.Nom,
                    BudgetPrevu = lot.TotalRevenueHT,
                    Niveau = Niveau,
                    TypeEtape = "Lot",
                    NombreSousEtapes = nombreSousEtapes
                });
            }

            return new ConversionPreviewDTO
            {
                DQE = new DQESummaryDTO
                {
                    Id = dqe.Id,
                    Reference = dqe.Reference,
                    Nom = dqe.Nom,
                    TotalRevenueHT = dqe.TotalRevenueHT,
                    LotsCount = dqe.Lots.Count,
                    ClientNom = dqe.Client.Nom
                },
                ProjetPrevu = projectPreview,
                EtapesPrevues = stagesPreview
            };
        }

        public async Task<bool> LinkDQEToExistingProjectAsync(
        int dqeId,
        int projetId,
        int utilisateurId)
        {
            // 1. Vérifier que le DQE peut être converti
            var (canConvert, reason) = await _dqeService.CanConvertToProjectAsync(dqeId);
            if (!canConvert)
            {
                throw new InvalidOperationException(reason);
            }

            // 2. Récupérer le DQE complet avec Lots, Chapters et Items
            var dqe = await _dqeService.GetByIdAsync(dqeId);
            if (dqe == null)
            {
                throw new InvalidOperationException("DQE introuvable");
            }

            // 3. Récupérer le projet
            var projet = await _projetService.GetByIdAsync(projetId);
            if (projet == null)
            {
                throw new InvalidOperationException("Projet introuvable");
            }

            // Vérifications de sécurité
            if (projet.Statut == "Termine" || projet.Statut == "Annule")
            {
                throw new InvalidOperationException("Impossible de lier à un projet terminé ou annulé");
            }

            if (projet.LinkedDqeId.HasValue)
            {
                throw new InvalidOperationException("Ce projet est déjà lié à un autre DQE");
            }

            // 4. Transaction pour garantir l'atomicité
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // 4a. Récupérer l'ordre maximum actuel des étapes du projet
                int ordreMax = await GetMaxOrdreEtapeAsync(conn, transaction, projetId);

                // 4b. Créer les étapes hiérarchiques (Lots + Items) à partir du DQE
                await CreateHierarchicalStagesForExistingProjectAsync(
                    conn,
                    transaction,
                    projetId,
                    dqe.Lots,
                    ordreMax + 1, // Commencer après les étapes existantes
                    projet.DateDebut ?? DateTime.UtcNow,
                    projet.DateFinPrevue ?? DateTime.UtcNow.AddDays(90),
                    dqe.Reference
                );

                // 4c. Mettre à jour le budget du projet
                await UpdateProjetBudgetAsync(
                    conn,
                    transaction,
                    projetId,
                    dqe.TotalRevenueHT
                );

                // 4d. Marquer le DQE comme converti et lier au projet
                await MarkDQEAsLinkedAsync(
                    conn,
                    transaction,
                    dqeId,
                    projetId,
                    projet.Numero,
                    utilisateurId
                );

                // 4e. Lier le projet au DQE
                await LinkProjectToDQEAsync(
                    conn,
                    transaction,
                    projetId,
                    dqeId,
                    dqe.Reference,
                    dqe.Nom,
                    dqe.TotalRevenueHT,
                    utilisateurId
                );

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new Exception($"Erreur lors de la liaison du DQE au projet: {ex.Message}", ex);
            }
        }
        // <summary>
        /// Récupère l'ordre maximum des étapes existantes dans le projet
        /// </summary>
        private async Task<int> GetMaxOrdreEtapeAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId)
        {
            using var cmd = new SqlCommand(@"
        SELECT ISNULL(MAX(Ordre), 0) 
        FROM EtapesProjets 
        WHERE ProjetId = @ProjetId 
        AND EtapeParentId IS NULL", conn, transaction);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);

            var result = await cmd.ExecuteScalarAsync();
            return result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// Crée la hiérarchie complète des étapes pour un projet existant
        /// Lots → Étapes principales (Niveau 1)
        /// Items → Sous-étapes (Niveau 2)
        /// </summary>
        private async Task CreateHierarchicalStagesForExistingProjectAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            List<DQELotDTO> lots,
            int ordreDebut,
            DateTime dateDebutProjet,
            DateTime dateFinProjet,
            string dqeReference)
        {
            int ordreEtapePrincipale = ordreDebut;
            var Niveau = 0;
            foreach (var lot in lots.OrderBy(l => l.Ordre))
            {
                Niveau++;
                // Étape principale (Lot)
                var etapeId = await InsertEtapePrincipaleForLinkingAsync(
                    conn,
                    transaction,
                    projetId,
                    lot,
                    ordreEtapePrincipale,
                    dateDebutProjet,
                    dateFinProjet,
                    lot.TotalRevenueHT,
                    0, // CoutReel
                    dqeReference,
                   Niveau
                );

                // Sous-étapes (Items)
                if (lot.Chapters != null && lot.Chapters.Any())
                {
                    int ordreSousEtape = 1;

                    foreach (var chapter in lot.Chapters.OrderBy(c => c.Ordre))
                    {
                        if (chapter.Items != null && chapter.Items.Any())
                        {
                            foreach (var item in chapter.Items.OrderBy(i => i.Ordre))
                            {
                                await InsertSousEtapeForLinkingAsync(
                                    conn,
                                    transaction,
                                    projetId,
                                    etapeId,
                                    item,
                                    chapter,
                                    lot,
                                    ordreSousEtape,
                                    dateDebutProjet,
                                    dateFinProjet,
                                    dqeReference,
                                    Niveau
                                );

                                ordreSousEtape++;
                            }
                        }
                    }
                }

                ordreEtapePrincipale++;
            }
        }

        /// <summary>
        /// Insère une étape principale (Lot DQE) pour liaison
        /// </summary>
        private async Task<int> InsertEtapePrincipaleForLinkingAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            DQELotDTO lot,
            int ordre,
            DateTime dateDebut,
            DateTime dateFinPrevue,
            decimal budgetPrevu,
            decimal coutReel,
            string dqeReference,
            int niveau)
        {
            using var cmd = new SqlCommand(@"
        INSERT INTO EtapesProjets (
            ProjetId, Nom, Description, Ordre,
            EtapeParentId, Niveau, TypeEtape,
            DateDebut, DateFinPrevue,
            Statut, PourcentageAvancement,
            BudgetPrevu, CoutReel, Depense,
            Unite, QuantitePrevue, PrixUnitairePrevu,
            ResponsableId, TypeResponsable, IdSousTraitant,
            LinkedDqeLotId, LinkedDqeLotCode, LinkedDqeLotName,
            LinkedDqeItemId, LinkedDqeItemCode,
            LinkedDqeChapterId, LinkedDqeChapterCode,
            LinkedDqeReference,
            EstActif, DateCreation, DateModification
        ) VALUES (
            @ProjetId, @Nom, @Description, @Ordre,
            NULL, @Niveau, 'Lot',
            @DateDebut, @DateFinPrevue,
            'NonCommence', 0,
            @BudgetPrevu, @CoutReel, 0,
            NULL, NULL, NULL,
            NULL, 'Interne', NULL,
            @LinkedDqeLotId, @LinkedDqeLotCode, @LinkedDqeLotName,
            NULL, NULL,
            NULL, NULL,
            @LinkedDqeReference,
            1, GETUTCDATE(), GETUTCDATE()
        );
        SELECT CAST(SCOPE_IDENTITY() AS INT);", conn, transaction);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@Nom", lot.Nom);
            cmd.Parameters.AddWithValue("@Description", lot.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", ordre);
            cmd.Parameters.AddWithValue("@Niveau", niveau);
            cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
            cmd.Parameters.AddWithValue("@DateFinPrevue", dateFinPrevue);
            cmd.Parameters.AddWithValue("@BudgetPrevu", budgetPrevu);
            cmd.Parameters.AddWithValue("@CoutReel", coutReel);
            cmd.Parameters.AddWithValue("@LinkedDqeLotId", lot.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeLotCode", lot.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeLotName", lot.Nom);
            cmd.Parameters.AddWithValue("@LinkedDqeReference", dqeReference);

            var etapeId = (int)await cmd.ExecuteScalarAsync();
            return etapeId;
        }

        /// <summary>
        /// Insère une sous-étape (Item DQE) pour liaison
        /// </summary>
        private async Task InsertSousEtapeForLinkingAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            int etapeParentId,
            DQEItemDTO item,
            DQEChapterDTO chapter,
            DQELotDTO lot,
            int ordre,
            DateTime dateDebut,
            DateTime dateFinPrevue,
            string dqeReference,
            int niveau)
        {
            using var cmd = new SqlCommand(@"
        INSERT INTO EtapesProjets (
            ProjetId, Nom, Description, Ordre,
            EtapeParentId, Niveau, TypeEtape,
            DateDebut, DateFinPrevue,
            Statut, PourcentageAvancement,
            BudgetPrevu, CoutReel, Depense,
            Unite, QuantitePrevue, QuantiteRealisee, PrixUnitairePrevu,
            ResponsableId, TypeResponsable, IdSousTraitant,
            LinkedDqeLotId, LinkedDqeLotCode, 
            LinkedDqeChapterId, LinkedDqeChapterCode,
            LinkedDqeItemId, LinkedDqeItemCode,
            LinkedDqeReference,
            EstActif, DateCreation, DateModification
        ) VALUES (
            @ProjetId, @Nom, @Description, @Ordre,
            @EtapeParentId, @Niveau, 'Item',
            @DateDebut, @DateFinPrevue,
            'NonCommence', 0,
            @BudgetPrevu, @CoutReel, 0,
            @Unite, @QuantitePrevue, 0, @PrixUnitairePrevu,
            NULL, 'Interne', NULL,
            @LinkedDqeLotId, @LinkedDqeLotCode, 
            @LinkedDqeChapterId, @LinkedDqeChapterCode,
            @LinkedDqeItemId, @LinkedDqeItemCode,
            @LinkedDqeReference,
            1, GETUTCDATE(), GETUTCDATE()
        );", conn, transaction);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@Nom", item.Designation);
            cmd.Parameters.AddWithValue("@Description", item.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ordre", ordre);
            cmd.Parameters.AddWithValue("@EtapeParentId", etapeParentId);
            cmd.Parameters.AddWithValue("@Niveau", niveau);
            cmd.Parameters.AddWithValue("@DateDebut", dateDebut);
            cmd.Parameters.AddWithValue("@DateFinPrevue", dateFinPrevue);
            cmd.Parameters.AddWithValue("@BudgetPrevu", item.TotalRevenueHT);
            cmd.Parameters.AddWithValue("@CoutReel", item.DeboursseSec);
            cmd.Parameters.AddWithValue("@Unite", item.Unite);
            cmd.Parameters.AddWithValue("@QuantitePrevue", item.Quantite);
            cmd.Parameters.AddWithValue("@PrixUnitairePrevu", item.PrixUnitaireHT);
            cmd.Parameters.AddWithValue("@LinkedDqeLotId", lot.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeLotCode", lot.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeChapterId", chapter.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeChapterCode", chapter.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeItemId", item.Id);
            cmd.Parameters.AddWithValue("@LinkedDqeItemCode", item.Code);
            cmd.Parameters.AddWithValue("@LinkedDqeReference", dqeReference);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Met à jour le budget du projet
        /// </summary>
        private async Task UpdateProjetBudgetAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            decimal budgetDQE)
        {
            using var cmd = new SqlCommand(@"
        UPDATE Projets 
        SET BudgetRevise = BudgetRevise + @BudgetDQE,
            DateModification = GETUTCDATE()
        WHERE Id = @ProjetId", conn, transaction);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@BudgetDQE", budgetDQE);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Marque le DQE comme lié à un projet
        /// </summary>
        private async Task MarkDQEAsLinkedAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int dqeId,
            int projetId,
            string projetNumero,
            int utilisateurId)
        {
            using var cmd = new SqlCommand(@"
        UPDATE DQE 
        SET IsConverted = 1,
            LinkedProjectId = @ProjetId,
            LinkedProjectNumber = @ProjetNumero,
            ConvertedAt = GETUTCDATE(),
            ConvertedById = @ConvertedById,
            DateModification = GETUTCDATE()
        WHERE Id = @DqeId", conn, transaction);

            cmd.Parameters.AddWithValue("@DqeId", dqeId);
            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@ProjetNumero", projetNumero);
            cmd.Parameters.AddWithValue("@ConvertedById", utilisateurId);

            await cmd.ExecuteNonQueryAsync();
        }



        /// <summary>
        /// Lie le projet au DQE
        /// </summary>
        private async Task LinkProjectToDQEAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            int dqeId,
            string dqeReference,
            string dqeNom,
            decimal dqeBudgetHT,
            int utilisateurId)
        {
            using var cmd = new SqlCommand(@"
        UPDATE Projets 
        SET LinkedDqeId = @DqeId,
            LinkedDqeReference = @DqeReference,
            LinkedDqeName = @DqeNom,
            LinkedDqeBudgetHT = @DqeBudgetHT,
            DqeConvertedAt = GETUTCDATE(),
            DqeConvertedById = @ConvertedById,
            DateModification = GETUTCDATE(),
            IsFromDqeConversion = @IsFromDqeConversion
        WHERE Id = @ProjetId", conn, transaction);

            cmd.Parameters.AddWithValue("@ProjetId", projetId);
            cmd.Parameters.AddWithValue("@DqeId", dqeId);
            cmd.Parameters.AddWithValue("@DqeReference", dqeReference);
            cmd.Parameters.AddWithValue("@DqeNom", dqeNom);
            cmd.Parameters.AddWithValue("@DqeBudgetHT", dqeBudgetHT);
            cmd.Parameters.AddWithValue("@ConvertedById", utilisateurId);
            cmd.Parameters.AddWithValue("@IsFromDqeConversion", 1);

            await cmd.ExecuteNonQueryAsync();
        }
        // DTOs pour preview
    }
}