using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.Devis
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var devis = await _devisService.GetByIdAsync(id);
                if (devis == null) return NotFound("Devis non trouvé");

                return Ok(devis);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDevisRequest model)
        {
            try
            {
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
                    TauxTVA = 20.00m,
                    UtilisateurCreation = 1 // TODO: Récupérer depuis JWT
                };

                // Mapper les lignes
                if (model.Lignes != null && model.Lignes.Any())
                {
                    devis.Lignes = model.Lignes.Select(l => new LigneDevis
                    {
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
                devis.Id = devisId;

                return CreatedAtAction(nameof(Get), new { id = devisId }, new
                {
                    message = "Devis créé avec succès",
                    devis
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateDevisRequest model)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null) return NotFound("Devis non trouvé");

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
                    existing.Lignes = model.Lignes.Select(l => new LigneDevis
                    {
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
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/envoyer")]
        public async Task<IActionResult> Envoyer(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null) return NotFound("Devis non trouvé");

                if (existing.Statut != "Brouillon")
                {
                    return BadRequest("Seuls les devis en brouillon peuvent être envoyés");
                }

                await _devisService.UpdateStatutAsync(id, "Envoye");

                // TODO: Implémenter l'envoi d'email avec PDF

                return Ok(new { message = "Devis envoyé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/valider")]
        public async Task<IActionResult> Valider(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null) return NotFound("Devis non trouvé");

                await _devisService.UpdateStatutAsync(id, "Valide");
                return Ok(new { message = "Devis validé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/refuser")]
        public async Task<IActionResult> Refuser(int id)
        {
            try
            {
                var existing = await _devisService.GetByIdAsync(id);
                if (existing == null) return NotFound("Devis non trouvé");

                await _devisService.UpdateStatutAsync(id, "Refuse");
                return Ok(new { message = "Devis refusé" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/dupliquer")]
        public async Task<IActionResult> Dupliquer(int id)
        {
            try
            {
                var original = await _devisService.GetByIdAsync(id);
                if (original == null) return NotFound("Devis non trouvé");

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
                    Lignes = original.Lignes?.Select(l => new LigneDevis
                    {
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
                    id = nouveauId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }
    }
}
