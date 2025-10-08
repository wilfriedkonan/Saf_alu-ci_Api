using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.Projets
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProjetsController : ControllerBase
    {
        private readonly ProjetService _projetService;

        public ProjetsController(ProjetService projetService)
        {
            _projetService = projetService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var projets = await _projetService.GetAllAsync();
                var result = projets.Select(p => new
                {
                    p.Id,
                    p.Numero,
                    p.Nom,
                    p.Statut,
                    p.PourcentageAvancement,
                    p.DateDebut,
                    p.DateFinPrevue,
                    p.BudgetInitial,
                    p.BudgetRevise,
                    p.CoutReel,
                    Client = p.Client != null ? new
                    {
                        p.Client.Id,
                        Nom = !string.IsNullOrEmpty(p.Client.RaisonSociale) ? p.Client.RaisonSociale :
                              $"{p.Client.Nom}".Trim()
                    } : null,
                    TypeProjet = p.TypeProjet?.Nom,
                    ChefProjet = p.ChefProjet != null ? $"{p.ChefProjet.Prenom} {p.ChefProjet.Nom}" : null,
                    StatutAvancement = GetStatutAvancement(p)
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
                var projet = await _projetService.GetByIdAsync(id);
                if (projet == null) return NotFound("Projet non trouvé");

                return Ok(projet);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetTypes()
        {
            try
            {
                var types = await _projetService.GetAllTypesAsync();
                return Ok(types);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpGet("en-retard")]
        public async Task<IActionResult> GetProjetsEnRetard()
        {
            try
            {
                var projets = await _projetService.GetProjetsEnRetardAsync();
                var result = projets.Select(p => new
                {
                    p.Id,
                    p.Numero,
                    p.Nom,
                    p.Statut,
                    p.PourcentageAvancement,
                    p.DateFinPrevue,
                    JoursRetard = p.DateFinPrevue.HasValue ? (DateTime.Now - p.DateFinPrevue.Value).Days : 0,
                    Client = p.Client != null ? new
                    {
                        p.Client.Id,
                        Nom = !string.IsNullOrEmpty(p.Client.RaisonSociale) ? p.Client.RaisonSociale :
                              $" {p.Client.Nom}".Trim()
                    } : null,
                    TypeProjet = p.TypeProjet?.Nom
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpGet("{id}/etapes")]
        public async Task<IActionResult> GetEtapes(int id)
        {
            try
            {
                var etapes = await _projetService.GetEtapesProjetAsync(id);
                return Ok(etapes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateProjetRequest model)
        {
            try
            {
                var projet = new Projet
                {
                    Nom = model.Nom,
                    Description = model.Description,
                    ClientId = model.ClientId,
                    TypeProjetId = model.TypeProjetId,
                    DevisId = model.DevisId,
                    Statut = "Planification",
                    DateDebut = model.DateDebut,
                    DateFinPrevue = model.DateFinPrevue,
                    BudgetInitial = model.BudgetInitial,
                    BudgetRevise = model.BudgetInitial, // Initialement égal au budget initial
                    CoutReel = 0,
                    AdresseChantier = model.AdresseChantier,
                    CodePostalChantier = model.CodePostalChantier,
                    VilleChantier = model.VilleChantier,
                    ChefProjetId = model.ChefProjetId,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    UtilisateurCreation = 1, // TODO: Récupérer depuis JWT
                    Actif = true
                };

                // Mapper les étapes
                if (model.Etapes != null && model.Etapes.Any())
                {
                    projet.Etapes = model.Etapes.Select(e => new EtapeProjet
                    {
                        Nom = e.Nom,
                        Description = e.Description,
                        DateDebut = e.DateDebut,
                        DateFinPrevue = e.DateFinPrevue,
                        BudgetPrevu = e.BudgetPrevu,
                        Statut = "NonCommence",
                        PourcentageAvancement = 0,
                        CoutReel = 0,
                        TypeResponsable = "Interne"
                    }).ToList();
                }

                var projetId = await _projetService.CreateAsync(projet);
                projet.Id = projetId;

                return CreatedAtAction(nameof(Get), new { id = projetId }, new
                {
                    message = "Projet créé avec succès",
                    projet
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateProjetRequest model)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                existing.Nom = model.Nom;
                existing.Description = model.Description;
                existing.ClientId = model.ClientId;
                existing.TypeProjetId = model.TypeProjetId;
                existing.DevisId = model.DevisId;
                existing.DateDebut = model.DateDebut;
                existing.DateFinPrevue = model.DateFinPrevue;
                existing.BudgetInitial = model.BudgetInitial;
                existing.AdresseChantier = model.AdresseChantier;
                existing.CodePostalChantier = model.CodePostalChantier;
                existing.VilleChantier = model.VilleChantier;
                existing.ChefProjetId = model.ChefProjetId;
                existing.DateModification = DateTime.UtcNow;

                // Mapper les étapes
                if (model.Etapes != null && model.Etapes.Any())
                {
                    existing.Etapes = model.Etapes.Select(e => new EtapeProjet
                    {
                        Nom = e.Nom,
                        Description = e.Description,
                        DateDebut = e.DateDebut,
                        DateFinPrevue = e.DateFinPrevue,
                        BudgetPrevu = e.BudgetPrevu,
                        Statut = "NonCommence",
                        PourcentageAvancement = 0,
                        CoutReel = 0,
                        TypeResponsable = "Interne"
                    }).ToList();
                }

                await _projetService.UpdateAsync(existing);
                return Ok(new { message = "Projet modifié avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                await _projetService.DeleteAsync(id);
                return Ok(new { message = "Projet supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/demarrer")]
        public async Task<IActionResult> Demarrer(int id)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                if (existing.Statut != "Planification")
                {
                    return BadRequest("Seuls les projets en planification peuvent être démarrés");
                }

                await _projetService.UpdateStatutAsync(id, "EnCours");
                return Ok(new { message = "Projet démarré avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/suspendre")]
        public async Task<IActionResult> Suspendre(int id)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                if (existing.Statut != "EnCours")
                {
                    return BadRequest("Seuls les projets en cours peuvent être suspendus");
                }

                await _projetService.UpdateStatutAsync(id, "Suspendu");
                return Ok(new { message = "Projet suspendu" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/reprendre")]
        public async Task<IActionResult> Reprendre(int id)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                if (existing.Statut != "Suspendu")
                {
                    return BadRequest("Seuls les projets suspendus peuvent être repris");
                }

                await _projetService.UpdateStatutAsync(id, "EnCours");
                return Ok(new { message = "Projet repris avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/terminer")]
        public async Task<IActionResult> Terminer(int id)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                if (existing.Statut == "Termine")
                {
                    return BadRequest("Le projet est déjà terminé");
                }

                await _projetService.UpdateStatutAsync(id, "Termine");
                await _projetService.UpdateAvancementAsync(id, 100);

                return Ok(new { message = "Projet terminé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("{id}/avancement")]
        public async Task<IActionResult> UpdateAvancement(int id, [FromBody] UpdateAvancementRequest model)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                if (model.PourcentageAvancement < 0 || model.PourcentageAvancement > 100)
                {
                    return BadRequest("Le pourcentage d'avancement doit être entre 0 et 100");
                }

                await _projetService.UpdateAvancementAsync(id, model.PourcentageAvancement);

                return Ok(new { message = "Avancement mis à jour avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("etapes/{etapeId}/avancement")]
        public async Task<IActionResult> UpdateAvancementEtape(int etapeId, [FromBody] UpdateAvancementRequest model)
        {
            try
            {
                if (model.PourcentageAvancement < 0 || model.PourcentageAvancement > 100)
                {
                    return BadRequest("Le pourcentage d'avancement doit être entre 0 et 100");
                }

                if (model.Note.HasValue && (model.Note.Value < 1 || model.Note.Value > 5))
                {
                    return BadRequest("La note doit être entre 1 et 5");
                }

                await _projetService.UpdateEtapeAvancementAsync(etapeId, model);

                return Ok(new { message = "Avancement de l'étape mis à jour avec succès" });
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
                var original = await _projetService.GetByIdAsync(id);
                if (original == null) return NotFound("Projet non trouvé");

                var nouveauProjet = new Projet
                {
                    Nom = $"Copie de {original.Nom}",
                    Description = original.Description,
                    ClientId = original.ClientId,
                    TypeProjetId = original.TypeProjetId,
                    Statut = "Planification",
                    BudgetInitial = original.BudgetInitial,
                    BudgetRevise = original.BudgetInitial,
                    CoutReel = 0,
                    AdresseChantier = original.AdresseChantier,
                    CodePostalChantier = original.CodePostalChantier,
                    VilleChantier = original.VilleChantier,
                    ChefProjetId = original.ChefProjetId,
                    PourcentageAvancement = 0,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    UtilisateurCreation = 1, // TODO: Récupérer depuis JWT
                    Actif = true,
                    Etapes = original.Etapes?.Select(e => new EtapeProjet
                    {
                        Nom = e.Nom,
                        Description = e.Description,
                        BudgetPrevu = e.BudgetPrevu,
                        Statut = "NonCommence",
                        PourcentageAvancement = 0,
                        CoutReel = 0,
                        TypeResponsable = e.TypeResponsable
                    }).ToList()
                };

                var nouveauId = await _projetService.CreateAsync(nouveauProjet);

                return CreatedAtAction(nameof(Get), new { id = nouveauId }, new
                {
                    message = "Projet dupliqué avec succès",
                    id = nouveauId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        private string GetStatutAvancement(Projet projet)
        {
            if (projet.Statut == "Termine") return "Terminé";
            if (projet.DateFinPrevue.HasValue && projet.DateFinPrevue.Value < DateTime.Now && projet.PourcentageAvancement < 100)
                return "En Retard";
            if (projet.PourcentageAvancement == 0) return "Non Commencé";
            if (projet.PourcentageAvancement < 100) return "En Cours";
            return "Terminé";
        }
    }
}