// Controllers/Devis/DevisController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DevisController : BaseController
    {
        private readonly DevisService _devisService;
        private readonly DevisPDFService _pdfService;

        public DevisController(DevisService devisService, DevisPDFService pdfService)
        {
            _devisService = devisService;
            _pdfService = pdfService;
        }

        /// <summary>
        /// Récupérer la liste de tous les devis (version simplifiée)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var devisList = await _devisService.GetAllAsync();
                return Ok(devisList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Récupérer un devis complet par son ID
        /// Retourne les sections avec leurs sous-sections (si présentes) et leurs lignes
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var devis = await _devisService.GetByIdAsync(id);
                if (devis == null)
                    return NotFound(new { message = "Devis non trouvé" });

                return Ok(devis);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Créer un nouveau devis avec sections et lignes.
        /// Les sous-sections sont facultatives : chaque section peut avoir
        /// des SousSections (avec leurs lignes) et/ou des Lignes directes.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDevisRequest model)
        {
            //try
            //{
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Données invalides",
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                    });
                }

                var utilisateurId = GetCurrentUserId();
                var devisId = await _devisService.CreateAsync(model, utilisateurId);

                return CreatedAtAction(nameof(Get), new { id = devisId }, new
                {
                    message = "Devis créé avec succès",
                    data = new { id = devisId }
                });
            //}
            //catch (Exception ex)
            //{
            //    return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            //}
        }

        /// <summary>
        /// Mettre à jour un devis existant.
        /// L'ensemble sections/sous-sections/lignes est remplacé en bloc.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDevisRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Données invalides",
                        errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))
                    });
                }

                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut == "Valide" || existing.Statut == "Envoye")
                    return BadRequest(new { message = "Ce devis ne peut plus être modifié" });

                await _devisService.UpdateAsync(id, model);
                return Ok(new { message = "Devis modifié avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Supprimer un devis (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut == "Valide")
                    return BadRequest(new { message = "Un devis validé ne peut pas être supprimé" });

                await _devisService.DeleteAsync(id);
                return Ok(new { message = "Devis supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Envoyer un devis au client
        /// </summary>
        [HttpPost("{id}/envoyer")]
        public async Task<IActionResult> Envoyer(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut != "Brouillon")
                    return BadRequest(new { message = "Seuls les devis en brouillon peuvent être envoyés" });

                await _devisService.UpdateStatutAsync(id, "Envoye");
                return Ok(new { message = "Devis envoyé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Valider un devis
        /// </summary>
        [HttpPost("{id}/valider")]
        public async Task<IActionResult> Valider(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut != "Envoye" && existing.Statut != "EnNegociation")
                    return BadRequest(new { message = "Seuls les devis envoyés ou en négociation peuvent être validés" });

                await _devisService.UpdateStatutAsync(id, "Valide");
                return Ok(new { message = "Devis validé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Refuser un devis
        /// </summary>
        [HttpPost("{id}/refuser")]
        public async Task<IActionResult> Refuser(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut == "Valide")
                    return BadRequest(new { message = "Un devis validé ne peut pas être refusé" });

                await _devisService.UpdateStatutAsync(id, "Refuse");
                return Ok(new { message = "Devis refusé" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Dupliquer un devis existant — conserve les sous-sections si présentes
        /// </summary>
        [HttpPost("{id}/dupliquer")]
        public async Task<IActionResult> Dupliquer(int id)
        {
            try
            {
                var original = await _devisService.GetByIdAsync(id);
                if (original == null)
                    return NotFound(new { message = "Devis non trouvé" });

                var utilisateurId = GetCurrentUserId();

                var duplicateRequest = new CreateDevisRequest
                {
                    ClientId = original.ClientId,
                    Titre = $"Copie de {original.Titre}",
                    Description = original.Description,
                    DateValidite = null,
                    Conditions = original.Conditions,
                    Notes = original.Notes,
                    Chantier = original.Chantier,
                    Contact = original.Contact,
                    QualiteMateriel = original.QualiteMateriel,
                    TypeVitrage = original.TypeVitrage,
                    RemiseValeur = original.RemiseValeur,
                    RemisePourcentage = original.RemisePourcentage,

                    Sections = original.Sections?.Select(s => new CreateDevisSectionRequest
                    {
                        Nom = s.Nom,
                        Ordre = s.Ordre,
                        Description = s.Description,

                        // Sous-sections (facultatives — null si la section n'en avait pas)
                        SousSections = s.SousSections.Any()
                            ? s.SousSections.Select(ss => new CreateDevisSousSectionRequest
                            {
                                Code = ss.Code,
                                Nom = ss.Nom,
                                Description = ss.Description,
                                Ordre = ss.Ordre,
                                Lignes = ss.Lignes.Select(MapLigneToRequest).ToList()
                            }).ToList()
                            : null,

                        // Lignes directes sur la section
                        Lignes = s.Lignes != null && s.Lignes.Any()
                            ? s.Lignes.Select(MapLigneToRequest).ToList()
                            : null
                    }).ToList()
                };

                var nouveauId = await _devisService.CreateAsync(duplicateRequest, utilisateurId);

                return CreatedAtAction(nameof(Get), new { id = nouveauId }, new
                {
                    message = "Devis dupliqué avec succès",
                    data = new { id = nouveauId }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Exporter un devis en PDF — les sous-sections apparaissent dans le PDF si présentes
        /// </summary>
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> ExporterPDF(int id)
        {
            try
            {
                var devis = await _devisService.GetByIdAsync(id);
                if (devis == null)
                    return NotFound(new { message = "Devis non trouvé" });

                var pdfBytes = _pdfService.GeneratePDF(devis);
                return File(pdfBytes, "application/pdf", $"Devis_{devis.Numero}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur lors de la génération du PDF", error = ex.Message });
            }
        }

        /// <summary>
        /// Rechercher des devis avec filtres et pagination
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Rechercher(
            [FromQuery] string? search,
            [FromQuery] string? statut,
            [FromQuery] int? clientId,
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10)
        {
            try
            {
                var result = await _devisService.RechercherAsync(new RechercheDevisRequest
                {
                    Search = search,
                    Statut = statut,
                    ClientId = clientId,
                    DateDebut = dateDebut,
                    DateFin = dateFin,
                    Page = page,
                    Limit = limit
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtenir les statistiques des devis
        /// </summary>
        [HttpGet("statistiques")]
        public async Task<IActionResult> GetStatistiques()
        {
            try
            {
                var stats = await _devisService.GetStatistiquesAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        // =====================================================
        // HELPERS PRIVÉS
        // =====================================================

        private static CreateLigneDevisRequest MapLigneToRequest(LigneDevisResponse l) =>
            new CreateLigneDevisRequest
            {
                TypeElement = l.TypeElement,
                Designation = l.Designation,
                Description = l.Description,
                Code = l.Code,
                Longueur = l.Longueur,
                Hauteur = l.Hauteur,
                Quantite = l.Quantite,
                Unite = l.Unite,
                PrixUnitaireHT = l.PrixUnitaireHT
            };

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 3;
        }
    }
}