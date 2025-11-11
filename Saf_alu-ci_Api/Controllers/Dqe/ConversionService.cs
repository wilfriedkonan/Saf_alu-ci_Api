using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Dqe;
using Saf_alu_ci_Api.Controllers.Projets;
using System.Data;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    /// <summary>
    /// Service responsable de la conversion DQE → Projet
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
        /// Convertit un DQE en projet avec toutes ses étapes
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

                // 3b. Créer les étapes depuis les lots
                if (request.ModeCreationEtapes == "automatique")
                {
                    var etapes = BuildStagesFromLots(
                        dqe.Lots,
                        projetId,
                        request.DateDebut,
                        request.DureeTotaleJours,
                        request.MethodeCalculDurees,
                        dqe.Reference
                    );

                    await CreateStagesWithTransactionAsync(conn, transaction, projetId, etapes);
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

        /// <summary>
        /// Génère une prévisualisation de la conversion sans créer le projet
        /// </summary>
        public async Task<ConversionPreviewDTO> PreviewConversionAsync(
            int dqeId,
            ConvertDQEToProjectRequest request)
        {
            // Récupérer le DQE
            var dqe = await _dqeService.GetByIdAsync(dqeId);
            if (dqe == null)
            {
                throw new InvalidOperationException("DQE introuvable");
            }

            // Générer le numéro de projet
            var numeroProjet = await _projetService.GenerateNumeroAsync();

            // Calculer la date de fin
            var dateFinPrevue = request.DateDebut.AddDays(request.DureeTotaleJours);

            // Construire la prévisualisation du projet
            var projectPreview = new ProjectPreviewDTO
            {
                Nom = string.IsNullOrEmpty(request.NomProjet) ? dqe.Nom : request.NomProjet,
                NumeroProjet = numeroProjet,
                BudgetInitial = dqe.TotalRevenueHT,
                DateDebut = request.DateDebut,
                DateFinPrevue = dateFinPrevue,
                DureeTotaleJours = request.DureeTotaleJours
            };

            // Calculer les étapes prévues
            var stagesPreview = CalculateStagesPreview(
                dqe.Lots,
                request.DateDebut,
                request.DureeTotaleJours,
                request.MethodeCalculDurees,
                dqe.TotalRevenueHT
            );

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

        // ========================================
        // MÉTHODES DE CONSTRUCTION
        // ========================================

        /// <summary>
        /// Construit un objet Projet depuis un DQE
        /// </summary>
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
                // Informations de lien DQE
                IsFromDqeConversion = true,
                LinkedDqeId = dqe.Id,
                LinkedDqeReference = dqe.Reference,
                LinkedDqeName = dqe.Nom,
                LinkedDqeBudgetHT = dqe.TotalRevenueHT,
                DqeConvertedAt = DateTime.UtcNow,
                DqeConvertedById = utilisateurId
            };
        }

        /// <summary>
        /// Construit les étapes depuis les lots DQE
        /// </summary>
        private List<EtapeProjet> BuildStagesFromLots(
            List<DQELotDTO> lots,
            int projetId,
            DateTime dateDebut,
            int dureeTotaleJours,
            string methodeCalculDurees,
            string dqeReference)
        {
            var etapes = new List<EtapeProjet>();
            var currentDate = dateDebut;

            // Calculer la durée de chaque étape
            var durees = CalculateStageDurations(
                lots.Select(l => l.TotalRevenueHT).ToList(),
                dureeTotaleJours,
                methodeCalculDurees
            );

            for (int i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                var dureeJours = durees[i];
                var dateFin = currentDate.AddDays(dureeJours);

                etapes.Add(new EtapeProjet
                {
                    ProjetId = projetId,
                    Nom = lot.Nom,
                    Description = lot.Description,
                    Ordre = lot.Ordre,
                    DateDebut = currentDate,
                    DateFinPrevue = dateFin,
                    Statut = "NonCommence",
                    PourcentageAvancement = 0,
                    BudgetPrevu = lot.TotalRevenueHT,
                    CoutReel = 0,
                    TypeResponsable = "Interne",
                    // Lien vers le lot DQE source
                    LinkedDqeLotId = lot.Id,
                    LinkedDqeLotCode = lot.Code,
                    LinkedDqeLotName = lot.Nom,
                    LinkedDqeReference = dqeReference
                });

                // Date de début de la prochaine étape = fin de l'étape actuelle + 1 jour
                currentDate = dateFin.AddDays(1);
            }

            return etapes;
        }

        /// <summary>
        /// Calcule la durée de chaque étape selon la méthode choisie
        /// </summary>
        private List<int> CalculateStageDurations(
            List<decimal> budgets,
            int dureeTotale,
            string methode)
        {
            var durees = new List<int>();
            var totalBudget = budgets.Sum();

            switch (methode)
            {
                case "proportionnelle":
                    // Durée proportionnelle au budget (minimum 5 jours)
                    foreach (var budget in budgets)
                    {
                        var pourcentage = budget / totalBudget;
                        var dureeCalculee = (int)Math.Round(dureeTotale * (double)pourcentage);
                        durees.Add(Math.Max(dureeCalculee, 5)); // Minimum 5 jours
                    }
                    break;

                case "egales":
                    // Durées égales pour toutes les étapes
                    var dureeEgale = dureeTotale / budgets.Count;
                    durees.AddRange(Enumerable.Repeat(Math.Max(dureeEgale, 5), budgets.Count));
                    break;

                case "personnalisee":
                    // Durées personnalisées (à implémenter selon besoin)
                    // Pour l'instant, utilise proportionnelle par défaut
                    goto case "proportionnelle";

                default:
                    goto case "proportionnelle";
            }

            // Ajuster la dernière étape pour atteindre exactement la durée totale
            var sommeDurees = durees.Sum();
            if (sommeDurees != dureeTotale && durees.Count > 0)
            {
                durees[durees.Count - 1] += (dureeTotale - sommeDurees);
            }

            return durees;
        }

        /// <summary>
        /// Calcule la prévisualisation des étapes
        /// </summary>
        private List<StagePreviewDTO> CalculateStagesPreview(
            List<DQELotDTO> lots,
            DateTime dateDebut,
            int dureeTotaleJours,
            string methodeCalculDurees,
            decimal totalBudget)
        {
            var stagesPreview = new List<StagePreviewDTO>();
            var currentDate = dateDebut;

            var durees = CalculateStageDurations(
                lots.Select(l => l.TotalRevenueHT).ToList(),
                dureeTotaleJours,
                methodeCalculDurees
            );

            for (int i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                var dureeJours = durees[i];
                var dateFin = currentDate.AddDays(dureeJours);

                stagesPreview.Add(new StagePreviewDTO
                {
                    Nom = lot.Nom,
                    Code = lot.Code,
                    BudgetPrevu = lot.TotalRevenueHT,
                    DureeJours = dureeJours,
                    DateDebut = currentDate,
                    DateFinPrevue = dateFin,
                    PourcentageBudget = (lot.TotalRevenueHT / totalBudget) * 100
                });

                currentDate = dateFin.AddDays(1);
            }

            return stagesPreview;
        }

        // ========================================
        // MÉTHODES DE BASE DE DONNÉES
        // ========================================

        /// <summary>
        /// Crée le projet avec transaction
        /// </summary>
        private async Task<int> CreateProjectWithTransactionAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            Projet projet)
        {
            // Générer le numéro de projet
            projet.Numero = await GenerateProjectNumberWithTransactionAsync(conn, transaction);

            using var cmd = new SqlCommand(@"
                INSERT INTO Projets (
                    Numero, Nom, Description, ClientId, TypeProjetId, Statut,
                    DateDebut, DateFinPrevue, BudgetInitial, BudgetRevise, CoutReel,
                    ChefProjetId, PourcentageAvancement,
                    DateCreation, DateModification, UtilisateurCreation, Actif,
                    LinkedDqeId, LinkedDqeReference, LinkedDqeName, LinkedDqeBudgetHT,
                    IsFromDqeConversion, DqeConvertedAt, DqeConvertedById
                ) VALUES (
                    @Numero, @Nom, @Description, @ClientId, @TypeProjetId, @Statut,
                    @DateDebut, @DateFinPrevue, @BudgetInitial, @BudgetRevise, @CoutReel,
                    @ChefProjetId, @PourcentageAvancement,
                    @DateCreation, @DateModification, @UtilisateurCreation, @Actif,
                    @LinkedDqeId, @LinkedDqeReference, @LinkedDqeName, @LinkedDqeBudgetHT,
                    @IsFromDqeConversion, @DqeConvertedAt, @DqeConvertedById
                );
                SELECT CAST(SCOPE_IDENTITY() as int)", conn, transaction);

            cmd.Parameters.AddWithValue("@Numero", projet.Numero);
            cmd.Parameters.AddWithValue("@Nom", projet.Nom);
            cmd.Parameters.AddWithValue("@Description", projet.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ClientId", projet.ClientId);
            cmd.Parameters.AddWithValue("@TypeProjetId", projet.TypeProjetId);
            cmd.Parameters.AddWithValue("@Statut", projet.Statut);
            cmd.Parameters.AddWithValue("@DateDebut", projet.DateDebut ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DateFinPrevue", projet.DateFinPrevue ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@BudgetInitial", projet.BudgetInitial);
            cmd.Parameters.AddWithValue("@BudgetRevise", projet.BudgetRevise);
            cmd.Parameters.AddWithValue("@CoutReel", projet.CoutReel);
            cmd.Parameters.AddWithValue("@ChefProjetId", projet.ChefProjetId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PourcentageAvancement", projet.PourcentageAvancement);
            cmd.Parameters.AddWithValue("@DateCreation", projet.DateCreation);
            cmd.Parameters.AddWithValue("@DateModification", projet.DateModification);
            cmd.Parameters.AddWithValue("@UtilisateurCreation", projet.UtilisateurCreation);
            cmd.Parameters.AddWithValue("@Actif", projet.Actif);
            cmd.Parameters.AddWithValue("@LinkedDqeId", projet.LinkedDqeId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeReference", projet.LinkedDqeReference ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeName", projet.LinkedDqeName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LinkedDqeBudgetHT", projet.LinkedDqeBudgetHT ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsFromDqeConversion", projet.IsFromDqeConversion);
            cmd.Parameters.AddWithValue("@DqeConvertedAt", projet.DqeConvertedAt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@DqeConvertedById", projet.DqeConvertedById ?? (object)DBNull.Value);

            return (int)await cmd.ExecuteScalarAsync();
        }

        /// <summary>
        /// Crée les étapes du projet avec transaction
        /// </summary>
        private async Task CreateStagesWithTransactionAsync(
            SqlConnection conn,
            SqlTransaction transaction,
            int projetId,
            List<EtapeProjet> etapes)
        {
            foreach (var etape in etapes)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO EtapesProjets (
                        ProjetId, Nom, Description, Ordre, DateDebut, DateFinPrevue,
                        Statut, PourcentageAvancement, BudgetPrevu, CoutReel,
                        ResponsableId, TypeResponsable,
                        LinkedDqeLotId, LinkedDqeLotCode, LinkedDqeLotName, LinkedDqeReference
                    ) VALUES (
                        @ProjetId, @Nom, @Description, @Ordre, @DateDebut, @DateFinPrevue,
                        @Statut, @PourcentageAvancement, @BudgetPrevu, @CoutReel,
                        @ResponsableId, @TypeResponsable,
                        @LinkedDqeLotId, @LinkedDqeLotCode, @LinkedDqeLotName, @LinkedDqeReference
                    )", conn, transaction);

                cmd.Parameters.AddWithValue("@ProjetId", projetId);
                cmd.Parameters.AddWithValue("@Nom", etape.Nom);
                cmd.Parameters.AddWithValue("@Description", etape.Description ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Ordre", etape.Ordre);
                cmd.Parameters.AddWithValue("@DateDebut", etape.DateDebut ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DateFinPrevue", etape.DateFinPrevue ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Statut", etape.Statut);
                cmd.Parameters.AddWithValue("@PourcentageAvancement", etape.PourcentageAvancement);
                cmd.Parameters.AddWithValue("@BudgetPrevu", etape.BudgetPrevu);
                cmd.Parameters.AddWithValue("@CoutReel", etape.CoutReel);
                cmd.Parameters.AddWithValue("@ResponsableId", etape.ResponsableId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@TypeResponsable", etape.TypeResponsable);
                cmd.Parameters.AddWithValue("@LinkedDqeLotId", etape.LinkedDqeLotId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeLotCode", etape.LinkedDqeLotCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeLotName", etape.LinkedDqeLotName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedDqeReference", etape.LinkedDqeReference ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Marque le DQE comme converti
        /// </summary>
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
            cmd.Parameters.AddWithValue("@LinkedProjectId", projetId);
            cmd.Parameters.AddWithValue("@LinkedProjectNumber", projetNumero);
            cmd.Parameters.AddWithValue("@ConvertedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@ConvertedById", utilisateurId);
            cmd.Parameters.AddWithValue("@DateModification", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Génère un numéro de projet unique avec transaction
        /// </summary>
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
    }
}