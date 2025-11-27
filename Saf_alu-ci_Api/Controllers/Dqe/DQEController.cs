using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Controllers.Dqe;
using Saf_alu_ci_Api.Controllers.Projets;

namespace Saf_alu_ci_Api.Controllers.Dqe
{
    [ApiController]
    [Route("api/[controller]")]
     [Authorize]
    public class DQEController : BaseController
    {
        private readonly DQEService _dqeService;
        private readonly ConversionService _conversionService;
        private readonly ProjetService _projetService;



        public DQEController(
            DQEService dqeService,
            ConversionService conversionService, ProjetService projetService)
        {
            _dqeService = dqeService;
            _conversionService = conversionService;
            _projetService = projetService;
        }

        // ========================================
        // ENDPOINTS CRUD DE BASE
        // ========================================

        /// <summary>
        /// Récupère tous les DQE avec filtres optionnels
        /// GET /api/dqe
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? statut = null,
            [FromQuery] bool? isConverted = null)
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut, isConverted);
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère un DQE par son ID avec structure complète
        /// GET /api/dqe/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var dqe = await _dqeService.GetByIdAsync(id);

                if (dqe == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                return Ok(dqe);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Crée un nouveau DQE
        /// POST /api/dqe
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDQERequest request)
        {
            try
            {
                // TODO: Récupérer l'utilisateur depuis JWT
                var utilisateurId = Convert.ToInt32(GetCurrentUserId());

                var dqeId = await _dqeService.CreateAsync(request, utilisateurId);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = dqeId },
                    new { message = "DQE créé avec succès", id = dqeId }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Met à jour un DQE existant
        /// PUT /api/dqe/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDQERequest request)
        {
            try
            {
                var existing = await _dqeService.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                // Vérifier que le DQE n'est pas converti (lecture seule)
                if (existing.IsConverted)
                {
                    return BadRequest(new { message = "Impossible de modifier un DQE déjà converti en projet" });
                }

                await _dqeService.UpdateAsync(id, request);

                return Ok(new { message = "DQE mis à jour avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Supprime un DQE (soft delete)
        /// DELETE /api/dqe/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _dqeService.GetByIdAsync(id);
                if (existing == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                // Vérifier que le DQE n'est pas converti
                if (existing.IsConverted)
                {
                    return BadRequest(new { message = "Impossible de supprimer un DQE déjà converti en projet" });
                }

                await _dqeService.DeleteAsync(id);

                return Ok(new { message = "DQE supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS DE VALIDATION
        // ========================================

        /// <summary>
        /// Valide un DQE (changement de statut)
        /// POST /api/dqe/{id}/validate
        /// </summary>
        [HttpPost("{id}/validate")]
        public async Task<IActionResult> Validate(int id, [FromBody] ValidateDQERequest? request = null)
        {
            try
            {
                var existing = await _dqeService.GetByIdAsync(id);
                var dqe = await _dqeService.GetByIdAsync(id);
                bool DebourseSecOk = false;

                foreach (var item_lot in dqe.Lots.ToList())
                {
                    foreach (var item_chap in item_lot.Chapters.ToList())
                    {
                        foreach (var item in item_chap.Items.ToList())
                        {

                            if (item.DeboursseSec <= 0.0m)
                            {
                                DebourseSecOk = true;
                                break;
                            }
                        }
                    }
                }
                if (DebourseSecOk != false)
                {
                    return BadRequest(new { message = "Veillez rensseigner tous les debourssé sec avant de convertir" });
                }
                if (existing == null)
                {
                    return NotFound(new { message = "DQE introuvable" });
                }

                if (existing.Statut == "validé")
                {
                    return BadRequest(new { message = "Le DQE est déjà validé" });
                }

                if (existing.IsConverted)
                {
                    return BadRequest(new { message = "Le DQE est déjà converti en projet" });
                }

                // TODO: Récupérer le nom de l'utilisateur depuis JWT
                int validePar = 3; // Placeholder

                var success = await _dqeService.ValidateAsync(id, validePar);

                if (success)
                {
                    return Ok(new { message = "DQE validé avec succès" });
                }
                else
                {
                    return BadRequest(new { message = "Impossible de valider le DQE" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS DE CONVERSION
        // ========================================

        /// <summary>
        /// Vérifie si un DQE peut être converti en projet
        /// GET /api/dqe/{id}/can-convert
        /// </summary>
        [HttpGet("{id}/can-convert")]
        public async Task<IActionResult> CanConvert(int id)
        {
            try
            {
                var (canConvert, reason) = await _dqeService.CanConvertToProjectAsync(id);

                return Ok(new
                {
                    canConvert,
                    reason,
                    message = canConvert ? "Le DQE peut être converti" : reason
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Génère une prévisualisation de la conversion DQE → Projet
        /// POST /api/dqe/{id}/conversion-preview
        /// </summary>
        [HttpPost("{id}/conversion-preview")]
        public async Task<IActionResult> GetConversionPreview(
            int id,
            [FromBody] ConvertDQEToProjectRequest request)
        {
            try
            {
                var preview = await _conversionService.PreviewConversionAsync(id, request);

                return Ok(preview);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Convertit un DQE en projet
        /// POST /api/dqe/{id}/convert-to-project
        /// </summary>
        [HttpPost("{id}/convert-to-project")]
        public async Task<IActionResult> ConvertToProject(
            int id,
            [FromBody] ConvertDQEToProjectRequest request)
        {
            try
            {
                // TODO: Récupérer l'utilisateur depuis JWT
                var utilisateurId = 3;
                request.ChefProjetId = 3;

                var projetId = await _conversionService.ConvertDQEToProjectAsync(id, request, utilisateurId);

                return Ok(new
                {
                    message = "DQE converti en projet avec succès",
                    projetId,
                    redirectUrl = $"/projets/{projetId}"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS SUPPLÉMENTAIRES
        // ========================================

        /// <summary>
        /// Récupère tous les DQE convertis
        /// GET /api/dqe/converted
        /// </summary>
        [HttpGet("converted")]
        public async Task<IActionResult> GetConverted()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(isConverted: true);
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère tous les DQE convertibles (validés et non convertis)
        /// GET /api/dqe/convertible
        /// </summary>
        [HttpGet("convertible")]
        public async Task<IActionResult> GetConvertible()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut: "validé", isConverted: false);
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère tous les DQE en brouillon
        /// GET /api/dqe/brouillon
        /// </summary>
        [HttpGet("brouillon")]
        public async Task<IActionResult> GetBrouillon()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut: "brouillon");
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupère tous les DQE validés
        /// GET /api/dqe/valide
        /// </summary>
        [HttpGet("valide")]
        public async Task<IActionResult> GetValide()
        {
            try
            {
                var dqeList = await _dqeService.GetAllAsync(statut: "validé");
                return Ok(dqeList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // ========================================
        // ENDPOINTS STATISTIQUES
        // ========================================

        /// <summary>
        /// Récupère les statistiques globales des DQE
        /// GET /api/dqe/stats
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var tous = await _dqeService.GetAllAsync();
                var convertis = tous.Where(d => d.IsConverted).ToList();
                var convertibles = tous.Where(d => d.ConversionStatus == "convertible").ToList();
                var brouillons = tous.Where(d => d.Statut == "brouillon").ToList();
                var valides = tous.Where(d => d.Statut == "validé").ToList();

                return Ok(new
                {
                    total = tous.Count,
                    converti = convertis.Count,
                    convertible = convertibles.Count,
                    brouillon = brouillons.Count,
                    valide = valides.Count,
                    totalBudgetHT = tous.Sum(d => d.TotalRevenueHT),
                    budgetConverti = convertis.Sum(d => d.TotalRevenueHT),
                    budgetConvertible = convertibles.Sum(d => d.TotalRevenueHT),
                    tauxConversion = tous.Count > 0 ?
                        Math.Round((decimal)convertis.Count / tous.Count * 100, 2) : 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPost("{dqeId}/link-to-project/{projetId}")]
        public async Task<IActionResult> LinkToExistingProject(int dqeId, int projetId)
        {
            try
            {
                // Vérifier que le DQE existe et peut être converti
                var (canConvert, reason) = await _dqeService.CanConvertToProjectAsync(dqeId);
                if (!canConvert)
                {
                    return BadRequest(new { message = reason });
                }

                // Vérifier que le projet existe et est disponible
                var projet = await _projetService.GetByIdAsync(projetId);
                if (projet == null)
                {
                    return NotFound(new { message = "Projet introuvable" });
                }

                if (projet.Statut == "Terminé" || projet.Statut == "Clôturé")
                {
                    return BadRequest(new { message = "Impossible de lier à un projet terminé" });
                }

                if (projet.LinkedDqeId.HasValue)
                {
                    return BadRequest(new { message = "Ce projet est déjà lié à un autre DQE" });
                }

                // Récupérer l'utilisateur depuis JWT
                var utilisateurId = Convert.ToInt32(GetCurrentUserId());

                // Lier le DQE au projet existant
                var success = await _conversionService.LinkDQEToExistingProjectAsync(
                    dqeId,
                    projetId,
                    utilisateurId
                );

                if (success)
                {
                    return Ok(new
                    {
                        message = "DQE lié au projet avec succès",
                        projetId,
                        redirectUrl = $"/projets/{projetId}"
                    });
                }
                else
                {
                    return BadRequest(new { message = "Erreur lors de la liaison du DQE au projet" });
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }
    }
}