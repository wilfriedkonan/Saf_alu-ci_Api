using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Controllers.Parametres;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    /// <summary>
    /// Controller complémentaire pour la gestion des paramètres système
    /// S'ajoute à UtilisateursController existant sans conflit
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ParametresController : ControllerBase
    {
        private readonly ParametresSystemeService _parametresService;
        private readonly UtilisateurService _utilisateurService;
        private readonly ILogger<ParametresController> _logger;

        public ParametresController(
            ParametresSystemeService parametresService,
            UtilisateurService utilisateurService,
            ILogger<ParametresController> logger)
        {
            _parametresService = parametresService;
            _utilisateurService = utilisateurService;
            _logger = logger;
        }

        // =============================================
        // ENDPOINTS: GESTION DES RÔLES
        // =============================================

        [HttpGet("roles")]
        public async Task<IActionResult> GetAllRoles()
        {
            try
            {
                var roles = await _parametresService.GetAllRolesWithStatsAsync();
                return Ok(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération rôles");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpGet("roles/{id}")]
        public async Task<IActionResult> GetRoleById(int id)
        {
            try
            {
                var role = await _utilisateurService.GetRoleByIdAsync(id);
                if (role == null)
                    return NotFound(new { success = false, message = "Rôle non trouvé" });

                var roleResponse = new RoleResponse
                {
                    Id = role.Id,
                    Nom = role.Nom,
                    Description = role.Description,
                    Permissions = role.GetPermissionsList(),
                    DateCreation = role.DateCreation,
                    Actif = role.Actif
                };

                return Ok(new { success = true, data = roleResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur récupération rôle {Id}", id);
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPost("roles")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Données invalides", errors = ModelState });

                var role = await _parametresService.CreateRoleAsync(request);
                return CreatedAtAction(nameof(GetRoleById), new { id = role.Id },
                    new { success = true, message = "Rôle créé avec succès", data = role });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création rôle");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("roles/{id}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                if (id != request.Id)
                    return BadRequest(new { success = false, message = "L'ID ne correspond pas" });

                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Données invalides", errors = ModelState });

                var role = await _parametresService.UpdateRoleAsync(request);
                return Ok(new { success = true, message = "Rôle mis à jour", data = role });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur mise à jour rôle");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("roles/{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                var success = await _parametresService.DeleteRoleAsync(id);
                if (!success)
                    return NotFound(new { success = false, message = "Rôle non trouvé" });

                return Ok(new { success = true, message = "Rôle supprimé" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur suppression rôle");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // ENDPOINTS: RECHERCHE AVANCÉE
        // =============================================

        [HttpPost("utilisateurs/search")]
        public async Task<IActionResult> SearchUtilisateurs([FromBody] SearchUtilisateursRequest request)
        {
            try
            {
                var result = await _parametresService.SearchUtilisateursAvanceeAsync(request);
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur recherche utilisateurs");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // =============================================
        // ENDPOINTS: STATISTIQUES
        // =============================================

        [HttpGet("utilisateurs/statistiques")]
        public async Task<IActionResult> GetStatistiquesUtilisateurs()
        {
            try
            {
                var stats = await _parametresService.GetStatistiquesDetailleesAsync();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur stats utilisateurs");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpGet("statistiques/roles")]
        public async Task<IActionResult> GetStatistiquesRoles()
        {
            try
            {
                var stats = await _utilisateurService.GetUserStatsByRoleAsync();
                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur stats rôles");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // =============================================
        // ENDPOINTS: PARAMÈTRES SYSTÈME
        // =============================================

        [HttpGet("systeme")]
        public async Task<IActionResult> GetAllParametres()
        {
            try
            {
                var parametres = await _parametresService.GetAllParametresAsync();
                return Ok(new { success = true, data = parametres });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur paramètres");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpGet("systeme/categories")]
        public async Task<IActionResult> GetParametresByCategorie()
        {
            try
            {
                var parametres = await _parametresService.GetParametresByCategorieAsync();
                return Ok(new { success = true, data = parametres });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur paramètres catégories");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpGet("systeme/{cle}")]
        public async Task<IActionResult> GetParametreByKey(string cle)
        {
            try
            {
                var parametre = await _parametresService.GetParametreByKeyAsync(cle);
                if (parametre == null)
                    return NotFound(new { success = false, message = "Paramètre non trouvé" });

                return Ok(new { success = true, data = parametre });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur paramètre {Cle}", cle);
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPut("systeme")]
        public async Task<IActionResult> UpdateParametre([FromBody] UpdateParametreRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Données invalides", errors = ModelState });

                int utilisateurId = GetCurrentUserId() ?? 1;
                var parametre = await _parametresService.UpdateParametreAsync(request, utilisateurId);

                if (parametre == null)
                    return NotFound(new { success = false, message = "Paramètre non trouvé" });

                return Ok(new { success = true, message = "Paramètre mis à jour", data = parametre });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur mise à jour paramètre");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // =============================================
        // ENDPOINTS: UTILITAIRES
        // =============================================

        [HttpGet("permissions")]
        public IActionResult GetAllPermissions()
        {
            try
            {
                var permissions = new Dictionary<string, List<string>>
                {
                    ["global"] = new List<string> { PermissionsList.ALL },
                    ["utilisateurs"] = new List<string> { PermissionsList.USERS_READ, PermissionsList.USERS_CREATE, PermissionsList.USERS_UPDATE, PermissionsList.USERS_DELETE, PermissionsList.USERS_ALL },
                    ["projets"] = new List<string> { PermissionsList.PROJECTS_READ, PermissionsList.PROJECTS_CREATE, PermissionsList.PROJECTS_UPDATE, PermissionsList.PROJECTS_DELETE, PermissionsList.PROJECTS_ALL, PermissionsList.PROJECTS_ASSIGNED, PermissionsList.PROJECTS_TASKS },
                    ["clients"] = new List<string> { PermissionsList.CLIENTS_READ, PermissionsList.CLIENTS_CREATE, PermissionsList.CLIENTS_UPDATE, PermissionsList.CLIENTS_DELETE, PermissionsList.CLIENTS_ALL },
                    ["factures"] = new List<string> { PermissionsList.INVOICES_READ, PermissionsList.INVOICES_CREATE, PermissionsList.INVOICES_UPDATE, PermissionsList.INVOICES_DELETE, PermissionsList.INVOICES_ALL },
                    ["finance"] = new List<string> { PermissionsList.FINANCE_READ, PermissionsList.FINANCE_CREATE, PermissionsList.FINANCE_UPDATE, PermissionsList.FINANCE_DELETE, PermissionsList.FINANCE_ALL },
                    ["dqe"] = new List<string> { PermissionsList.DQE_READ, PermissionsList.DQE_CREATE, PermissionsList.DQE_UPDATE, PermissionsList.DQE_DELETE, PermissionsList.DQE_ALL },
                    ["stock"] = new List<string> { PermissionsList.STOCK_READ, PermissionsList.STOCK_CREATE, PermissionsList.STOCK_UPDATE, PermissionsList.STOCK_DELETE, PermissionsList.STOCK_ALL },
                    ["rapports"] = new List<string> { PermissionsList.REPORTS_FINANCE, PermissionsList.REPORTS_PROJECTS, PermissionsList.REPORTS_ALL },
                    ["documents"] = new List<string> { PermissionsList.DOCUMENTS_READ, PermissionsList.DOCUMENTS_UPLOAD, PermissionsList.DOCUMENTS_DELETE },
                    ["parametres"] = new List<string> { PermissionsList.SETTINGS_READ, PermissionsList.SETTINGS_UPDATE },
                    ["taches"] = new List<string> { PermissionsList.TASKS_UPDATE }
                };

                return Ok(new { success = true, data = permissions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur permissions");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpGet("roles/liste")]
        [AllowAnonymous]
        public IActionResult GetRolesList()
        {
            try
            {
                var rolesList = RolePermissions.RoleLabels.Select(r => new
                {
                    key = r.Key,
                    label = r.Value,
                    permissions = RolePermissions.GetPermissions(r.Key),
                    hierarchy = RolePermissions.RoleHierarchy.TryGetValue(r.Key, out var h) ? h : 0
                }).OrderBy(r => r.hierarchy);

                return Ok(new { success = true, data = rolesList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur liste rôles");
                return StatusCode(500, new { success = false, message = $"Erreur serveur : {ex.Message}" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("id") ?? User.FindFirst("userId") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }

            return null;
        }
    }
}
