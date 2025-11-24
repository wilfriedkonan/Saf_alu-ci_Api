using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Factures;

namespace Saf_alu_ci_Api.Controllers.Projets
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
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
                    p.DateDebut,
                    p.DateFinPrevue,
                    p.BudgetInitial,
                    p.BudgetRevise,
                    p.PourcentageAvancement,
                    p.CoutReel,
                    Client = p.Client != null ? new
                    {
                        p.Client.Id,
                        Nom = !string.IsNullOrEmpty(p.Client.Nom) ? p.Client.Nom :
                              $"{p.Client.Nom}".Trim(),
                    } : null,
                    TypeProjet = p.TypeProjet?.Nom,
                    ChefProjet = p.ChefProjet != null ? $"{p.ChefProjet.Prenom} {p.ChefProjet.Nom}" : null,
                    StatutAvancement = GetStatutAvancement(p),
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

                if (projet == null)
                    return NotFound("Projet non trouvé");

                // Vérifier que la liste n'est pas nulle et pas vide
                if (projet.Etapes == null || !projet.Etapes.Any())
                {
                    projet.PourcentageAvancement = 0;
                    return Ok(projet);
                }

                // Calcul des pourcentages
                var totalAvancement = projet.Etapes.Sum(x => x.PourcentageAvancement);
                var totalEtapes = projet.Etapes.Count;

                // division sécurisée
                var moyennePourcent = (double)totalAvancement / totalEtapes;

                projet.PourcentageAvancement = Convert.ToInt32(moyennePourcent);

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
                    //TypeProjetId = model.TypeProjetId,
                    DevisId = model.DevisId,
                    Statut = "Planification",
                    DateDebut = model.DateDebut,
                    DateFinPrevue = model.DateFinPrevue,
                    BudgetInitial = model.BudgetInitial,
                    BudgetRevise = model.BudgetInitial, // Initialement égal au budget initial
                    // CoutReel sera calculé automatiquement depuis les étapes
                    AdresseChantier = model.AdresseChantier,
                    CodePostalChantier = model.CodePostalChantier,
                    VilleChantier = model.VilleChantier,
                    ChefProjetId = 3,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    UtilisateurCreation = 3, // TODO: Récupérer depuis JWT
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
                        CoutReel = Convert.ToDecimal(e.CoutReel),
                        Statut = "NonCommence",
                        PourcentageAvancement = 0,
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
        public async Task<IActionResult> Update(int id, [FromBody] UpdateProjetRequest model)
        {
            try
            {
                // Vérifier que le projet existe
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound("Projet non trouvé");

                // Effectuer la mise à jour partielle
                var success = await _projetService.UpdateAsync(id, model);

                if (!success)
                    return StatusCode(500, "Erreur lors de la mise à jour du projet");

                // Récupérer le projet mis à jour
                var updated = await _projetService.GetByIdAsync(id);

                return Ok(new
                {
                    message = "Projet mis à jour avec succès",
                    projet = updated
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        // ========================================
        // ENDPOINT BONUS - Mise à jour d'un champ spécifique (PATCH)
        // ========================================

        /// <summary>
        /// Met à jour un champ spécifique d'un projet
        /// </summary>
        /// <example>
        /// PATCH /api/projets/1/statut
        /// Body: { "value": "EnCours" }
        /// </example>
        [HttpPatch("{id}/{field}")]
        public async Task<IActionResult> UpdateField(int id, string field, [FromBody] UpdateFieldRequest request)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound("Projet non trouvé");

                var updateRequest = new UpdateProjetRequest();

                // Mapper le champ à mettre à jour
                switch (field.ToLower())
                {
                    case "nom":
                        updateRequest.Nom = request.Value?.ToString();
                        break;
                    case "description":
                        updateRequest.Description = request.Value?.ToString();
                        break;
                    case "statut":
                        updateRequest.Statut = request.Value?.ToString();
                        break;
                    case "dateDebut":
                        if (DateTime.TryParse(request.Value?.ToString(), out DateTime dateDebut))
                            updateRequest.DateDebut = dateDebut;
                        break;
                    case "dateFinPrevue":
                        if (DateTime.TryParse(request.Value?.ToString(), out DateTime dateFinPrevue))
                            updateRequest.DateFinPrevue = dateFinPrevue;
                        break;
                    case "budgetInitial":
                        if (decimal.TryParse(request.Value?.ToString(), out decimal budgetInitial))
                            updateRequest.BudgetInitial = budgetInitial;
                        break;
                    case "budgetRevise":
                        if (decimal.TryParse(request.Value?.ToString(), out decimal budgetRevise))
                            updateRequest.BudgetRevise = budgetRevise;
                        break;
                    case "chefProjetId":
                        if (int.TryParse(request.Value?.ToString(), out int chefProjetId))
                            updateRequest.ChefProjetId = chefProjetId;
                        break;
                    case "adresseChantier":
                        updateRequest.AdresseChantier = request.Value?.ToString();
                        break;
                    case "villeChantier":
                        updateRequest.VilleChantier = request.Value?.ToString();
                        break;
                    case "codePostalChantier":
                        updateRequest.CodePostalChantier = request.Value?.ToString();
                        break;
                    default:
                        return BadRequest($"Champ '{field}' non modifiable ou invalide");
                }

                var success = await _projetService.UpdateAsync(id, updateRequest);

                if (!success)
                    return StatusCode(500, "Erreur lors de la mise à jour");

                return Ok(new
                {
                    message = $"Champ '{field}' mis à jour avec succès",
                    field = field,
                    newValue = request.Value
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        /// <summary>
        /// DTO pour mise à jour d'un champ unique
        /// </summary>
        public class UpdateFieldRequest
        {
            public object? Value { get; set; }
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
                    return BadRequest("Seuls les projets EnCours peuvent être suspendus");
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

        [HttpPost("{id}/recalculer-cout-reel")]
        public async Task<IActionResult> RecalculateCoutReel(int id)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                await _projetService.RecalculateCoutReelAsync(id);

                return Ok(new { message = "Coût réel recalculé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpPost("etapes/{etapeId}/depense")]
        public async Task<IActionResult> UpdateEtapeDepense(int etapeId, [FromBody] UpdateDepenseRequest request)
        {
            try
            {
                if (request.Montant <= 0)
                {
                    return BadRequest("Le montant doit être supérieur à 0");
                }

                if (string.IsNullOrEmpty(request.TypeOperation) ||
                    (request.TypeOperation.ToUpper() != "DEBIT" && request.TypeOperation.ToUpper() != "CREDIT"))
                {
                    return BadRequest("TypeOperation doit être 'Debit' ou 'Credit'");
                }

                await _projetService.UpdateEtapeDepenseAsync(etapeId, request);

                // Récupérer la nouvelle dépense totale
                var nouvelleDepense = await _projetService.GetEtapeDepenseAsync(etapeId);

                return Ok(new
                {
                    message = $"Dépense {request.TypeOperation.ToLower()}ée avec succès",
                    etapeId = etapeId,
                    montantOperation = request.Montant,
                    typeOperation = request.TypeOperation,
                    depenseTotale = nouvelleDepense,
                    referenceTransaction = request.ReferenceTransaction
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpGet("etapes/{etapeId}/depense")]
        public async Task<IActionResult> GetEtapeDepense(int etapeId)
        {
            try
            {
                var depense = await _projetService.GetEtapeDepenseAsync(etapeId);

                return Ok(new
                {
                    etapeId = etapeId,
                    depenseTotale = depense
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur serveur : {ex.Message}");
            }
        }

        [HttpGet("{id}/depense-totale")]
        public async Task<IActionResult> GetProjetDepenseTotale(int id)
        {
            try
            {
                var existing = await _projetService.GetByIdAsync(id);
                if (existing == null) return NotFound("Projet non trouvé");

                var depenseTotale = await _projetService.GetProjetDepenseTotaleAsync(id);

                return Ok(new
                {
                    projetId = id,
                    depenseTotale = depenseTotale,
                    budgetPrevu = existing.BudgetRevise,
                    ecart = existing.BudgetRevise - depenseTotale,
                    pourcentageUtilise = existing.BudgetRevise > 0
                        ? Math.Round((depenseTotale / existing.BudgetRevise) * 100, 2)
                        : 0
                });
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
            if (projet.PourcentageAvancement < 100) return "EnCours";
            return "Terminé";
        }
        [HttpGet("statistiques")]
        public async Task<IActionResult> GetStatistiques()
        {
            try
            {
                var factures = await _projetService.GetAllAsync();
                var stats = new
                {
                    // Golbal 
                    totalProjets = factures.Count(),
                    retardProjet = factures.Where(x => x.DateFinPrevue < DateTime.Now).Count(),
                    budgetTotal = factures.Sum(f => f.BudgetInitial),
                    ProjetEncour = factures.Where(x => x.Statut == "EnCours").Count(),

                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

    }
}