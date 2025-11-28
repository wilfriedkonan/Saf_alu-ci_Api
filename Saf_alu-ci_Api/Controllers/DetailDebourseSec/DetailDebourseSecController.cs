using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Controllers.Dqe;

namespace Saf_alu_ci_Api.Controllers.DetailDebourseSec
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DetailDebourseSecController : ControllerBase
    {
        private readonly DetailDebourseSecService _service;
        private readonly ILogger<DetailDebourseSecController> _logger;

        public DetailDebourseSecController(
            DetailDebourseSecService service,
            ILogger<DetailDebourseSecController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // =============================================
        // GET: api/DetailDebourseSec/item/{itemId}
        // Récupère tous les détails d'un item
        // =============================================
        [HttpGet("item/{itemId}")]
        public async Task<ActionResult<List<DetailDebourseSecResponse>>> GetByItemId(int itemId)
        {
            try
            {
                var details = await _service.GetByItemIdAsync(itemId);

                var response = details.Select(d => new DetailDebourseSecResponse
                {
                    Id = d.Id,
                    ItemId = d.ItemId,
                    TypeDepense = d.TypeDepense,
                    TypeDepenseLabel = TypeDepenseEnum.GetLabel(d.TypeDepense),
                    Designation = d.Designation,
                    Description = d.Description,
                    Ordre = d.Ordre,
                    Unite = d.Unite,
                    Quantite = d.Quantite,
                    PrixUnitaireHT = d.PrixUnitaireHT,
                    MontantHT = d.MontantHT,
                    Coefficient = d.Coefficient,
                    ReferenceExterne = d.ReferenceExterne,
                    Notes = d.Notes,
                    DateCreation = d.DateCreation,
                    DateModification = d.DateModification,
                    Actif = d.Actif
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = response,
                    count = response.Count,
                    message = $"{response.Count} détail(s) trouvé(s)"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur GET détails item {itemId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la récupération des détails",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // GET: api/DetailDebourseSec/{id}
        // Récupère un détail par ID
        // =============================================
        [HttpGet("{id}")]
        public async Task<ActionResult<DetailDebourseSecResponse>> GetById(int id)
        {
            try
            {
                var detail = await _service.GetByIdAsync(id);
                if (detail == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Détail {id} introuvable"
                    });
                }

                var response = new DetailDebourseSecResponse
                {
                    Id = detail.Id,
                    ItemId = detail.ItemId,
                    TypeDepense = detail.TypeDepense,
                    TypeDepenseLabel = TypeDepenseEnum.GetLabel(detail.TypeDepense),
                    Designation = detail.Designation,
                    Description = detail.Description,
                    Ordre = detail.Ordre,
                    Unite = detail.Unite,
                    Quantite = detail.Quantite,
                    PrixUnitaireHT = detail.PrixUnitaireHT,
                    MontantHT = detail.MontantHT,
                    Coefficient = detail.Coefficient,
                    ReferenceExterne = detail.ReferenceExterne,
                    Notes = detail.Notes,
                    DateCreation = detail.DateCreation,
                    DateModification = detail.DateModification,
                    Actif = detail.Actif
                };

                return Ok(new
                {
                    success = true,
                    data = response,
                    message = "Détail récupéré avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur GET détail {id}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la récupération du détail",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // POST: api/DetailDebourseSec/item/{itemId}
        // Crée un nouveau détail pour un item
        // =============================================
        [HttpPost("item/{itemId}")]
        public async Task<ActionResult<DetailDebourseSecResponse>> Create(
            int itemId,
            [FromBody] CreateDetailDebourseSecRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Données invalides",
                        errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                    });
                }

                var detail = await _service.CreateAsync(itemId, request);

                var response = new DetailDebourseSecResponse
                {
                    Id = detail.Id,
                    ItemId = detail.ItemId,
                    TypeDepense = detail.TypeDepense,
                    TypeDepenseLabel = TypeDepenseEnum.GetLabel(detail.TypeDepense),
                    Designation = detail.Designation,
                    Description = detail.Description,
                    Ordre = detail.Ordre,
                    Unite = detail.Unite,
                    Quantite = detail.Quantite,
                    PrixUnitaireHT = detail.PrixUnitaireHT,
                    MontantHT = detail.MontantHT,
                    Coefficient = detail.Coefficient,
                    ReferenceExterne = detail.ReferenceExterne,
                    Notes = detail.Notes,
                    DateCreation = detail.DateCreation,
                    DateModification = detail.DateModification,
                    Actif = detail.Actif
                };

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = detail.Id },
                    new
                    {
                        success = true,
                        data = response,
                        message = "Détail de déboursé créé avec succès"
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur POST détail item {itemId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la création du détail",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // PUT: api/DetailDebourseSec/{id}
        // Met à jour un détail
        // =============================================
        [HttpPut("{id}")]
        public async Task<ActionResult<DetailDebourseSecResponse>> Update(
            int id,
            [FromBody] UpdateDetailDebourseSecRequest request)
        {
            try
            {
                var detail = await _service.UpdateAsync(id, request);

                var response = new DetailDebourseSecResponse
                {
                    Id = detail.Id,
                    ItemId = detail.ItemId,
                    TypeDepense = detail.TypeDepense,
                    TypeDepenseLabel = TypeDepenseEnum.GetLabel(detail.TypeDepense),
                    Designation = detail.Designation,
                    Description = detail.Description,
                    Ordre = detail.Ordre,
                    Unite = detail.Unite,
                    Quantite = detail.Quantite,
                    PrixUnitaireHT = detail.PrixUnitaireHT,
                    MontantHT = detail.MontantHT,
                    Coefficient = detail.Coefficient,
                    ReferenceExterne = detail.ReferenceExterne,
                    Notes = detail.Notes,
                    DateCreation = detail.DateCreation,
                    DateModification = detail.DateModification,
                    Actif = detail.Actif
                };

                return Ok(new
                {
                    success = true,
                    data = response,
                    message = "Détail mis à jour avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur PUT détail {id}");

                if (ex.Message.Contains("introuvable"))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = ex.Message
                    });
                }

                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la mise à jour du détail",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // DELETE: api/DetailDebourseSec/{id}
        // Supprime un détail (soft delete)
        // =============================================
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var success = await _service.DeleteAsync(id);

                if (!success)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = $"Détail {id} introuvable"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Détail supprimé avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur DELETE détail {id}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la suppression du détail",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // GET: api/DetailDebourseSec/item/{itemId}/recapitulatif
        // Récapitulatif des déboursés par type
        // =============================================
        [HttpGet("item/{itemId}/recapitulatif")]
        public async Task<ActionResult<RecapitulatifDebourseSecResponse>> GetRecapitulatif(int itemId)
        {
            try
            {
                var recap = await _service.GetRecapitulatifAsync(itemId);

                return Ok(new
                {
                    success = true,
                    data = recap,
                    message = "Récapitulatif généré avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur récapitulatif item {itemId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la génération du récapitulatif",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // POST: api/DetailDebourseSec/copy/{sourceItemId}/to/{targetItemId}
        // Copie les détails d'un item vers un autre
        // =============================================
        [HttpPost("copy/{sourceItemId}/to/{targetItemId}")]
        public async Task<ActionResult> CopyDetails(int sourceItemId, int targetItemId)
        {
            try
            {
                var copiedDetails = await _service.CopyDetailsAsync(sourceItemId, targetItemId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        sourceItemId,
                        targetItemId,
                        copiedCount = copiedDetails.Count
                    },
                    message = $"{copiedDetails.Count} détail(s) copié(s) avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur copie détails {sourceItemId} -> {targetItemId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la copie des détails",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // DELETE: api/DetailDebourseSec/item/{itemId}/all
        // Supprime tous les détails d'un item
        // =============================================
        [HttpDelete("item/{itemId}/all")]
        public async Task<ActionResult> DeleteAll(int itemId)
        {
            try
            {
                var deletedCount = await _service.DeleteAllByItemIdAsync(itemId);

                return Ok(new
                {
                    success = true,
                    data = new { itemId, deletedCount },
                    message = $"{deletedCount} détail(s) supprimé(s) avec succès"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erreur suppression tous détails item {itemId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Erreur lors de la suppression des détails",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // GET: api/DetailDebourseSec/types
        // Liste des types de dépense disponibles
        // =============================================
        [HttpGet("types")]
        [AllowAnonymous]
        public ActionResult GetTypes()
        {
            var types = TypeDepenseEnum.All.Select(t => new
            {
                value = t,
                label = TypeDepenseEnum.GetLabel(t)
            });

            return Ok(new
            {
                success = true,
                data = types,
                message = "Types de dépense récupérés"
            });
        }
    }
}