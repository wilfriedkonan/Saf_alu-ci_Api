// Controllers/Devis/DevisController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    [ApiController]
    [Route("api/[controller]")]
   // [Authorize]
    public class DevisController : ControllerBase
    {
        private readonly DevisService _devisService;

        public DevisController(DevisService devisService)
        {
            _devisService = devisService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var devisList = await _devisService.GetAllAsync();
                var result = devisList.Select(d => new
                {
                    d.Id,
                    d.Numero,
                    d.Titre,
                    d.Statut,
                    d.MontantTTC,
                    d.DateCreation,
                    d.DateValidite,
                    Client = d.Client != null ? new
                    {
                        d.Client.Id,
                        Nom = !string.IsNullOrEmpty(d.Client.RaisonSociale) ? d.Client.RaisonSociale :
                              $"{d.Client.Prenom} {d.Client.Nom}".Trim()
                    } : null
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

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

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDevisRequest model)
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

                var devis = new Devis
                {
                    ClientId = model.ClientId,
                    Titre = model.Titre,
                    Description = model.Description,
                    Statut = "Brouillon",
                    DateCreation = DateTime.UtcNow,
                    DateValidite = model.DateValidite,
                    DateModification = DateTime.UtcNow,
                    Conditions = model.Conditions,
                    Notes = model.Notes,
                    TauxTVA = 18.00m, // 18% par défaut
                    UtilisateurCreation = 1 // TODO: Récupérer depuis JWT
                };

                // Mapper les lignes
                if (model.Lignes != null && model.Lignes.Any())
                {
                    devis.Lignes = model.Lignes.Select((l, index) => new LigneDevis
                    {
                        Ordre = index + 1,
                        Designation = l.Designation,
                        Description = l.Description,
                        Quantite = l.Quantite,
                        Unite = l.Unite,
                        PrixUnitaireHT = l.PrixUnitaireHT
                    }).ToList();

                    // Calculer les montants
                    devis.MontantHT = devis.Lignes.Sum(l => l.TotalHT);
                    devis.MontantTTC = devis.MontantHT * (1 + devis.TauxTVA / 100);
                }

                var devisId = await _devisService.CreateAsync(devis);

                return CreatedAtAction(nameof(Get), new { id = devisId }, new
                {
                    message = "Devis créé avec succès",
                    data = new { id = devisId }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateDevisRequest model)
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

                // Vérifier si le devis peut être modifié
                if (existing.Statut == "Valide" || existing.Statut == "Envoye")
                {
                    return BadRequest(new { message = "Ce devis ne peut plus être modifié" });
                }

                existing.ClientId = model.ClientId;
                existing.Titre = model.Titre;
                existing.Description = model.Description;
                existing.DateValidite = model.DateValidite;
                existing.Conditions = model.Conditions;
                existing.Notes = model.Notes;
                existing.DateModification = DateTime.UtcNow;

                // Mapper les lignes
                if (model.Lignes != null && model.Lignes.Any())
                {
                    existing.Lignes = model.Lignes.Select((l, index) => new LigneDevis
                    {
                        Ordre = index + 1,
                        Designation = l.Designation,
                        Description = l.Description,
                        Quantite = l.Quantite,
                        Unite = l.Unite,
                        PrixUnitaireHT = l.PrixUnitaireHT
                    }).ToList();

                    // Recalculer les montants
                    existing.MontantHT = existing.Lignes.Sum(l => l.TotalHT);
                    existing.MontantTTC = existing.MontantHT * (1 + existing.TauxTVA / 100);
                }

                await _devisService.UpdateAsync(existing);
                return Ok(new { message = "Devis modifié avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                // Vérifier si le devis peut être supprimé
                if (existing.Statut == "Valide")
                {
                    return BadRequest(new { message = "Un devis validé ne peut pas être supprimé" });
                }

                await _devisService.DeleteAsync(id);
                return Ok(new { message = "Devis supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPost("{id}/envoyer")]
        public async Task<IActionResult> Envoyer(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut != "Brouillon")
                {
                    return BadRequest(new { message = "Seuls les devis en brouillon peuvent être envoyés" });
                }

                await _devisService.UpdateStatutAsync(id, "Envoye");

                // TODO: Implémenter l'envoi d'email avec PDF

                return Ok(new { message = "Devis envoyé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPost("{id}/valider")]
        public async Task<IActionResult> Valider(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut != "Envoye" && existing.Statut != "EnNegociation")
                {
                    return BadRequest(new { message = "Seuls les devis envoyés ou en négociation peuvent être validés" });
                }

                await _devisService.UpdateStatutAsync(id, "Valide");
                return Ok(new { message = "Devis validé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPost("{id}/refuser")]
        public async Task<IActionResult> Refuser(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Devis non trouvé" });

                if (existing.Statut == "Valide")
                {
                    return BadRequest(new { message = "Un devis validé ne peut pas être refusé" });
                }

                await _devisService.UpdateStatutAsync(id, "Refuse");
                return Ok(new { message = "Devis refusé" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

        [HttpPost("{id}/dupliquer")]
        public async Task<IActionResult> Dupliquer(int id)
        {
            try
            {
                var original = await _devisService.GetByIdAsync(id);
                if (original == null)
                    return NotFound(new { message = "Devis non trouvé" });

                var nouveauDevis = new Devis
                {
                    ClientId = original.ClientId,
                    Titre = $"Copie de {original.Titre}",
                    Description = original.Description,
                    Statut = "Brouillon",
                    MontantHT = original.MontantHT,
                    TauxTVA = original.TauxTVA,
                    MontantTTC = original.MontantTTC,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    Conditions = original.Conditions,
                    Notes = original.Notes,
                    UtilisateurCreation = 1, // TODO: Récupérer depuis JWT
                    Lignes = original.Lignes?.Select((l, index) => new LigneDevis
                    {
                        Ordre = index + 1,
                        Designation = l.Designation,
                        Description = l.Description,
                        Quantite = l.Quantite,
                        Unite = l.Unite,
                        PrixUnitaireHT = l.PrixUnitaireHT
                    }).ToList()
                };

                var nouveauId = await _devisService.CreateAsync(nouveauDevis);

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

        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> ExporterPDF(int id)
        {
            try
            {
                var devis = await _devisService.GetByIdAsync(id);
                if (devis == null)
                    return NotFound(new { message = "Devis non trouvé" });

                // TODO: Implémenter la génération PDF
                // Pour l'instant, retourner un placeholder
                var pdfBytes = await _devisService.GeneratePDFAsync(devis);

                return File(pdfBytes, "application/pdf", $"devis-{devis.Numero}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erreur serveur : {ex.Message}" });
            }
        }

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
    }
}