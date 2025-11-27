using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.Dashboard
{

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : BaseController
    {
        private readonly DashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            DashboardService dashboardService,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        /// <summary>
        /// Récupère les statistiques globales pour admin/super_admin
        /// Utilise la vue v_KPIDashboard existante avec calculs supplémentaires
        /// </summary>
        [HttpGet("statistiques-globales")]
        public async Task<ActionResult> GetStatistiquesGlobales()
        {
            try
            {
                var stats = await _dashboardService.GetStatistiquesGlobalesAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques globales");
                return StatusCode(500, new { message = "Erreur lors de la récupération des statistiques" });
            }
        }

        /// <summary>
        /// Récupère les statistiques pour un chef de projet
        /// </summary>
        [HttpGet("statistiques-chef-projet")]
        public async Task<ActionResult> GetStatistiquesChefProjet()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var stats = await _dashboardService.GetStatistiquesChefProjetAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques chef de projet");
                return StatusCode(500, new { message = "Erreur lors de la récupération des statistiques" });
            }
        }

        /// <summary>
        /// Récupère les statistiques pour un commercial
        /// </summary>
        [HttpGet("statistiques-commercial")]
        public async Task<ActionResult> GetStatistiquesCommercial()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var stats = await _dashboardService.GetStatistiquesCommercialAsync(userId);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques commercial");
                return StatusCode(500, new { message = "Erreur lors de la récupération des statistiques" });
            }
        }

        /// <summary>
        /// Récupère les statistiques pour un comptable
        /// Utilise la vue v_KPIDashboard existante
        /// </summary>
        [HttpGet("statistiques-comptable")]
        public async Task<ActionResult> GetStatistiquesComptable()
        {
            try
            {
                var stats = await _dashboardService.GetStatistiquesComptableAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des statistiques comptable");
                return StatusCode(500, new { message = "Erreur lors de la récupération des statistiques" });
            }
        }

        /// <summary>
        /// Récupère l'évolution du chiffre d'affaires
        /// Utilise votre méthode existante GetRevenusParMoisAsync
        /// </summary>
        [HttpGet("evolution-chiffre-affaires")]
        public async Task<ActionResult> GetEvolutionChiffreAffaires([FromQuery] int mois = 6)
        {
            try
            {
                var data = await _dashboardService.GetRevenusParMoisAsync(mois);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de l'évolution du CA");
                return StatusCode(500, new { message = "Erreur lors de la récupération des données" });
            }
        }

        /// <summary>
        /// Récupère la répartition des projets par statut
        /// Nouvelle méthode qui complète GetRepartitionProjetsParTypeAsync
        /// </summary>
        [HttpGet("repartition-projets")]
        public async Task<ActionResult> GetRepartitionProjets()
        {
            try
            {
                var data = await _dashboardService.GetRepartitionProjetsParStatutAsync();
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la répartition des projets");
                return StatusCode(500, new { message = "Erreur lors de la récupération des données" });
            }
        }

        /// <summary>
        /// Récupère les projets nécessitant une attention
        /// Filtre automatiquement par chef de projet si rôle = chef_projet
        /// </summary>
        [HttpGet("projets-alerte")]
        public async Task<ActionResult> GetProjetsAlerte()
        {
            try
            {
                var role = User.FindFirst("Role")?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                int? chefProjetId = role == "chef_projet" ? userId : null;
                var alertes = await _dashboardService.GetProjetsAlerteAsync(chefProjetId);

                return Ok(alertes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des alertes projets");
                return StatusCode(500, new { message = "Erreur lors de la récupération des alertes" });
            }
        }

        /// <summary>
        /// Récupère les activités récentes (devis, projets, factures)
        /// </summary>
        [HttpGet("activites-recentes")]
        public async Task<ActionResult> GetActivitesRecentes([FromQuery] int limite = 10)
        {
            try
            {
                var activites = await _dashboardService.GetActivitesRecentesAsync(limite);
                return Ok(activites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des activités récentes");
                return StatusCode(500, new { message = "Erreur lors de la récupération des activités" });
            }
        }

        /// <summary>
        /// Récupère toutes les données du dashboard en une seule requête
        /// Optimisé pour réduire le nombre d'appels API depuis le frontend
        /// </summary>
        [HttpGet("donnees-completes")]
        public async Task<ActionResult> GetDonneesCompletes()
        {
            try
            {
                var role = User.FindFirst("Role")?.Value;
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                object stats = null;

                // Récupérer les statistiques selon le rôle
                switch (role)
                {
                    case "super_admin":
                    case "admin":
                        stats = await _dashboardService.GetStatistiquesGlobalesAsync();
                        break;
                    case "chef_projet":
                        stats = await _dashboardService.GetStatistiquesChefProjetAsync(userId);
                        break;
                    case "commercial":
                        stats = await _dashboardService.GetStatistiquesCommercialAsync(userId);
                        break;
                    case "comptable":
                        stats = await _dashboardService.GetStatistiquesComptableAsync();
                        break;
                }

                // Activités récentes (communes à tous)
                var activites = await _dashboardService.GetActivitesRecentesAsync(10);

                // Données spécifiques selon le rôle
                List<ChartData> evolutionCA = null;
                List<ChartData> repartitionProjets = null;
                List<AlerteProjet> alertes = null;

                // Graphiques pour admin, super_admin et comptable
                if (role == "super_admin" || role == "admin" || role == "comptable")
                {
                    evolutionCA = await _dashboardService.GetRevenusParMoisAsync(6);
                    repartitionProjets = await _dashboardService.GetRepartitionProjetsParStatutAsync();
                }

                // Alertes pour admin, super_admin et chef_projet
                if (role == "super_admin" || role == "admin" || role == "chef_projet")
                {
                    int? chefProjetId = role == "chef_projet" ? userId : null;
                    alertes = await _dashboardService.GetProjetsAlerteAsync(chefProjetId);
                }

                return Ok(new
                {
                    statistiques = stats,
                    activitesRecentes = activites,
                    evolutionChiffreAffaires = evolutionCA,
                    repartitionProjets = repartitionProjets,
                    projetsAlerte = alertes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des données complètes du dashboard");
                return StatusCode(500, new { message = "Erreur lors de la récupération des données" });
            }
        }

        // =============================================
        // ENDPOINTS EXISTANTS (si vous voulez les exposer via ce contrôleur)
        // =============================================

        /// <summary>
        /// Récupère les KPIs de base depuis la vue v_KPIDashboard
        /// Endpoint compatible avec votre code existant
        /// </summary>
        [HttpGet("kpis")]
        public async Task<ActionResult> GetKPIs()
        {
            try
            {
                var kpis = await _dashboardService.GetKPIsAsync();
                return Ok(kpis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des KPIs");
                return StatusCode(500, new { message = "Erreur lors de la récupération des KPIs" });
            }
        }

        /// <summary>
        /// Récupère les projets actifs depuis la vue v_ProjetsActifs
        /// Endpoint compatible avec votre code existant
        /// </summary>
        [HttpGet("projets-actifs")]
        public async Task<ActionResult> GetProjetsActifs()
        {
            try
            {
                var projets = await _dashboardService.GetProjetsActifsAsync();
                return Ok(projets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération des projets actifs");
                return StatusCode(500, new { message = "Erreur lors de la récupération des projets" });
            }
        }

        /// <summary>
        /// Récupère la répartition des projets par type
        /// Endpoint compatible avec votre code existant
        /// </summary>
        [HttpGet("repartition-projets-type")]
        public async Task<ActionResult> GetRepartitionProjetsParType()
        {
            try
            {
                var data = await _dashboardService.GetRepartitionProjetsParTypeAsync();
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la récupération de la répartition par type");
                return StatusCode(500, new { message = "Erreur lors de la récupération des données" });
            }
        }
    }
}

