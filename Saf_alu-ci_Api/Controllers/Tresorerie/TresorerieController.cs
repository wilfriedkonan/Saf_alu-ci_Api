using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.Globalization;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.Tresorerie
{

    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class TresorerieController : ControllerBase
    {
        private readonly TresorerieService _tresorerieService;

        public TresorerieController(TresorerieService tresorerieService)
        {
            _tresorerieService = tresorerieService;
        }

        // =============================================
        // ENDPOINTS COMPTES
        // =============================================

        /// <summary>
        /// Récupère tous les comptes avec leurs soldes actuels
        /// </summary>
        [HttpGet("comptes")]
        public async Task<IActionResult> GetComptes([FromQuery] string? typeCompte = null)
        {
            try
            {
                var comptes = await _tresorerieService.GetAllComptesAsync();

                if (!string.IsNullOrEmpty(typeCompte))
                {
                    comptes = comptes.Where(c => c.TypeCompte.Equals(typeCompte, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var result = comptes.Select(c => new
                {
                    c.Id,
                    c.Nom,
                    c.TypeCompte,
                    c.Numero,
                    c.Banque,
                    c.SoldeInitial,
                    c.SoldeActuel,
                    DifferenceInitiale = c.SoldeActuel - c.SoldeInitial,
                    c.DateCreation,
                    c.Actif,
                    StatutSolde = GetStatutSolde(c.SoldeActuel, c.TypeCompte),
                    CouleurStatut = GetCouleurSolde(c.SoldeActuel)
                }).OrderByDescending(c => c.SoldeActuel);

                var resume = new
                {
                    Comptes = result,
                    Resume = new
                    {
                        NombreComptes = comptes.Count,
                        SoldeTotal = comptes.Sum(c => c.SoldeActuel),
                        SoldeInitialTotal = comptes.Sum(c => c.SoldeInitial),
                        VariationTotale = comptes.Sum(c => c.SoldeActuel - c.SoldeInitial),
                        RepartitionParType = comptes.GroupBy(c => c.TypeCompte)
                                                   .Select(g => new
                                                   {
                                                       Type = g.Key,
                                                       Nombre = g.Count(),
                                                       Solde = g.Sum(c => c.SoldeActuel)
                                                   })
                    }
                };

                return Ok(resume);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère un compte par son ID
        /// </summary>
        [HttpGet("comptes/{id}")]
        public async Task<IActionResult> GetCompte(int id)
        {
            try
            {
                var compte = await _tresorerieService.GetCompteByIdAsync(id);
                if (compte == null) return NotFound(new { message = "Compte non trouvé" });

                var result = new
                {
                    compte.Id,
                    compte.Nom,
                    compte.TypeCompte,
                    compte.Numero,
                    compte.Banque,
                    compte.SoldeInitial,
                    compte.SoldeActuel,
                    DifferenceInitiale = compte.SoldeActuel - compte.SoldeInitial,
                    PourcentageEvolution = compte.SoldeInitial != 0 ?
                        (compte.SoldeActuel - compte.SoldeInitial) / compte.SoldeInitial * 100 : 0,
                    compte.DateCreation,
                    compte.Actif
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Crée un nouveau compte
        /// </summary>
        [HttpPost("comptes")]
        public async Task<IActionResult> CreateCompte([FromBody] CreateCompteRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var compte = new Compte
                {
                    Nom = model.Nom,
                    TypeCompte = model.TypeCompte,
                    Numero = model.Numero,
                    Banque = model.Banque,
                    SoldeInitial = model.SoldeInitial,
                    SoldeActuel = model.SoldeInitial,
                    DateCreation = DateTime.UtcNow,
                    Actif = true
                };

                var compteId = await _tresorerieService.CreateCompteAsync(compte);
                compte.Id = compteId;

                return CreatedAtAction(nameof(GetCompte), new { id = compteId }, new
                {
                    message = "Compte créé avec succès",
                    id = compteId,
                    nom = compte.Nom,
                    soldeInitial = compte.SoldeInitial
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Met à jour un compte
        /// </summary>
        [HttpPut("comptes/{id}")]
        public async Task<IActionResult> UpdateCompte(int id, [FromBody] UpdateCompteRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existing = await _tresorerieService.GetCompteByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Compte non trouvé" });

                existing.Nom = model.Nom;
                existing.Numero = model.Numero;
                existing.Banque = model.Banque;

                await _tresorerieService.UpdateCompteAsync(existing);
                return Ok(new { message = "Compte modifié avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Supprime (désactive) un compte
        /// </summary>
        [HttpDelete("comptes/{id}")]
        public async Task<IActionResult> DeleteCompte(int id)
        {
            try
            {
                var existing = await _tresorerieService.GetCompteByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Compte non trouvé" });

                await _tresorerieService.DeleteCompteAsync(id);
                return Ok(new { message = "Compte supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // ENDPOINTS MOUVEMENTS
        // =============================================

        /// <summary>
        /// Récupère les mouvements financiers avec filtres avancés
        /// </summary>
        [HttpGet("mouvements")]
        public async Task<IActionResult> GetMouvements(
            [FromQuery] int? compteId,
            [FromQuery] string? typeMouvement,
            [FromQuery] string? categorie,
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int nbJours = 30,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var mouvements = await _tresorerieService.GetMouvementsAsync(compteId, nbJours, typeMouvement, categorie, dateDebut, dateFin);

                // Pagination
                var totalMouvements = mouvements.Count;
                var mouvementsPagines = mouvements.Skip((page - 1) * pageSize)
                                                  .Take(pageSize)
                                                  .Select(m => new
                                                  {
                                                      m.Id,
                                                      m.TypeMouvement,
                                                      m.Categorie,
                                                      m.Libelle,
                                                      m.Description,
                                                      m.Montant,
                                                      m.DateMouvement,
                                                      m.DateSaisie,
                                                      m.ModePaiement,
                                                      m.Reference,
                                                      Compte = new { m.CompteId, Nom = "Nom du compte" }, // TODO: Navigation property
                                                      CompteDestination = m.CompteDestinationId.HasValue ? new { m.CompteDestinationId, Nom = "Nom compte dest" } : null,
                                                      Couleur = GetCouleurTypeMouvement(m.TypeMouvement)
                                                  });

                return Ok(new
                {
                    mouvements = mouvementsPagines,
                    pagination = new
                    {
                        page,
                        pageSize,
                        total = totalMouvements,
                        totalPages = (int)Math.Ceiling((double)totalMouvements / pageSize)
                    },
                    resume = new
                    {
                        totalEntrees = mouvements.Where(m => m.TypeMouvement == "Entree").Sum(m => m.Montant),
                        totalSorties = mouvements.Where(m => m.TypeMouvement == "Sortie").Sum(m => m.Montant),
                        totalVirements = mouvements.Where(m => m.TypeMouvement == "Virement").Sum(m => m.Montant),
                        nombreMouvements = totalMouvements
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère un mouvement par son ID
        /// </summary>
        [HttpGet("mouvements/{id}")]
        public async Task<IActionResult> GetMouvement(int id)
        {
            try
            {
                var mouvement = await _tresorerieService.GetMouvementByIdAsync(id);
                if (mouvement == null) return NotFound(new { message = "Mouvement non trouvé" });

                return Ok(mouvement);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Crée un nouveau mouvement financier
        /// </summary>
        [HttpPost("mouvements")]
        public async Task<IActionResult> CreateMouvement([FromBody] CreateMouvementRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var utilisateurId = GetCurrentUserId();
                var mouvement = new MouvementFinancier
                {
                    CompteId = model.CompteId,
                    TypeMouvement = model.TypeMouvement,
                    Categorie = model.Categorie,
                    FactureId = model.FactureId,
                    ProjetId = model.ProjetId,
                    EtapeProjetId = model.EtapeProjetId,
                    SousTraitantId = model.SousTraitantId,
                    Libelle = model.Libelle,
                    Description = model.Description,
                    Montant = model.Montant,
                    DateMouvement = model.DateMouvement,
                    DateSaisie = DateTime.UtcNow,
                    ModePaiement = model.ModePaiement,
                    Reference = model.Reference,
                    CompteDestinationId = model.CompteDestinationId,
                    UtilisateurCreation = utilisateurId
                };

                var mouvementId = await _tresorerieService.CreateMouvementAsync(mouvement);
                mouvement.Id = mouvementId;

                return Ok(new
                {
                    message = "Mouvement enregistré avec succès",
                    id = mouvementId,
                    type = mouvement.TypeMouvement,
                    montant = mouvement.Montant
                });
            }

            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Effectue un virement entre comptes
        /// </summary>
        [HttpPost("virements")]
        public async Task<IActionResult> CreateVirement([FromBody] VirementRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var utilisateurId = GetCurrentUserId();
                var success = await _tresorerieService.CreateVirementAsync(model, utilisateurId);

                if (!success)
                {
                    return BadRequest(new { message = "Solde insuffisant pour effectuer le virement" });
                }

                return Ok(new
                {
                    message = "Virement effectué avec succès",
                    montant = model.Montant,
                    compteSource = model.CompteSourceId,
                    compteDestination = model.CompteDestinationId
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Corrige le solde d'un compte
        /// </summary>
        [HttpPost("comptes/{id}/correction-solde")]
        public async Task<IActionResult> CorrigerSolde(int id, [FromBody] CorrectionSoldeRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var compte = await _tresorerieService.GetCompteByIdAsync(id);
                if (compte == null) return NotFound(new { message = "Compte non trouvé" });

                var utilisateurId = GetCurrentUserId();
                await _tresorerieService.CorrigerSoldeAsync(id, model, utilisateurId);

                return Ok(new
                {
                    message = "Solde corrigé avec succès",
                    ancienSolde = compte.SoldeActuel,
                    nouveauSolde = model.NouveauSolde,
                    motif = model.MotifCorrection
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // ENDPOINTS STATISTIQUES ET ANALYTICS
        // =============================================

        /// <summary>
        /// Récupère les statistiques complètes de la trésorerie
        /// </summary>
        [HttpGet("statistiques")]
        public async Task<IActionResult> GetStatistiques()
        {
            try
            {
                var stats = await _tresorerieService.GetStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère le tableau de bord de la trésorerie
        /// </summary>
        [HttpGet("tableau-de-bord")]
        public async Task<IActionResult> GetTableauDeBord()
        {
            try
            {
                var tableau = await _tresorerieService.GetTableauDeBordAsync();
                return Ok(tableau);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les catégories de mouvements utilisées
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _tresorerieService.GetCategoriesUtiliseesAsync();
                return Ok(new { categories });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les soldes par type de compte
        /// </summary>
        [HttpGet("soldes-par-type")]
        public async Task<IActionResult> GetSoldesParType()
        {
            try
            {
                var soldes = await _tresorerieService.GetSoldesParTypeAsync();
                return Ok(soldes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Vérifie si un compte a un solde suffisant
        /// </summary>
        [HttpGet("comptes/{id}/verifier-solde")]
        public async Task<IActionResult> VerifierSolde(int id, [FromQuery] decimal montant)
        {
            try
            {
                var soldeSuffisant = await _tresorerieService.VerifierSoldeSuffisantAsync(id, montant);
                var compte = await _tresorerieService.GetCompteByIdAsync(id);

                if (compte == null) return NotFound(new { message = "Compte non trouvé" });

                return Ok(new
                {
                    comptId = id,
                    nomCompte = compte.Nom,
                    soldeActuel = compte.SoldeActuel,
                    montantDemande = montant,
                    soldeSuffisant,
                    montantManquant = soldeSuffisant ? 0 : montant - compte.SoldeActuel
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // ENDPOINTS RAPPORTS ET EXPORTS
        // =============================================

        /// <summary>
        /// Génère un rapport PDF de trésorerie (placeholder)
        /// </summary>
        [HttpGet("rapports/pdf")]
        public async Task<IActionResult> GenererRapportPDF([FromQuery] DateTime? dateDebut, [FromQuery] DateTime? dateFin)
        {
            try
            {
                dateDebut ??= DateTime.Now.AddDays(-30);
                dateFin ??= DateTime.Now;

                // TODO: Implémenter la génération PDF
                return Ok(new
                {
                    message = "Génération PDF à implémenter",
                    periode = new { dateDebut, dateFin },
                    format = "PDF"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Exporte les mouvements au format Excel/CSV (placeholder)
        /// </summary>
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExporterExcel(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] int? compteId,
            [FromQuery] string format = "xlsx")
        {
            try
            {
                dateDebut ??= DateTime.Now.AddDays(-30);
                dateFin ??= DateTime.Now;

                var mouvements = await _tresorerieService.GetMouvementsAsync(compteId, 0, null, null, dateDebut, dateFin);

                // TODO: Implémenter l'export Excel/CSV
                return Ok(new
                {
                    message = $"Export {format.ToUpper()} à implémenter",
                    nombreMouvements = mouvements.Count,
                    periode = new { dateDebut, dateFin },
                    format = format.ToUpper()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // ENDPOINTS DE MAINTENANCE ET UTILITAIRES
        // =============================================

        /// <summary>
        /// Obtient les types de comptes disponibles
        /// </summary>
        [HttpGet("types-comptes")]
        public IActionResult GetTypesComptes()
        {
            try
            {
                var types = new[]
                {
                    new { Value = "Courant", Label = "Compte Courant", Description = "Compte bancaire principal" },
                    new { Value = "Epargne", Label = "Compte Épargne", Description = "Compte d'épargne ou de réserve" },
                    new { Value = "Caisse", Label = "Caisse", Description = "Caisse physique ou petite monnaie" }
                };

                return Ok(types);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtient les types de mouvements disponibles
        /// </summary>
        [HttpGet("types-mouvements")]
        public IActionResult GetTypesMouvements()
        {
            try
            {
                var types = new[]
                {
                    new { Value = "Entree", Label = "Entrée", Description = "Encaissement ou recette", Color = "#10b981" },
                    new { Value = "Sortie", Label = "Sortie", Description = "Décaissement ou dépense", Color = "#ef4444" },
                    new { Value = "Virement", Label = "Virement", Description = "Transfert entre comptes", Color = "#3b82f6" }
                };

                return Ok(types);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtient les catégories prédéfinies pour les mouvements
        /// </summary>
        [HttpGet("categories-predefinies")]
        public IActionResult GetCategoriesPredefinies()
        {
            try
            {
                var categories = new[]
                {
                    new { Value = "Facture client", Label = "Facture Client", Type = "Entree" },
                    new { Value = "Paiement sous-traitant", Label = "Paiement Sous-traitant", Type = "Sortie" },
                    new { Value = "Charges sociales", Label = "Charges Sociales", Type = "Sortie" },
                    new { Value = "Assurances", Label = "Assurances", Type = "Sortie" },
                    new { Value = "Location", Label = "Location", Type = "Sortie" },
                    new { Value = "Fournitures", Label = "Fournitures", Type = "Sortie" },
                    new { Value = "Carburant", Label = "Carburant", Type = "Sortie" },
                    new { Value = "Maintenance", Label = "Maintenance", Type = "Sortie" },
                    new { Value = "Frais bancaires", Label = "Frais Bancaires", Type = "Sortie" },
                    new { Value = "Autre", Label = "Autre", Type = "Mixte" }
                };

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint de santé/diagnostic du module trésorerie
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var comptes = await _tresorerieService.GetAllComptesAsync();
                var stats = await _tresorerieService.GetStatsAsync();

                var health = new
                {
                    status = "Healthy",
                    timestamp = DateTime.UtcNow,
                    module = "Tresorerie",
                    checks = new
                    {
                        database = new { status = "OK", details = "Connexion base de données réussie" },
                        comptes = new { status = "OK", count = comptes.Count, details = $"{comptes.Count} comptes actifs" },
                        soldeTotal = new { status = "OK", value = stats.SoldeTotal, details = $"Solde total: {stats.SoldeTotal:C}" },
                        mouvementsMois = new { status = "OK", value = stats.Indicateurs.NombreMouvementsMois, details = $"{stats.Indicateurs.NombreMouvementsMois} mouvements ce mois" }
                    }
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Unhealthy",
                    timestamp = DateTime.UtcNow,
                    module = "Tresorerie",
                    error = ex.Message
                });
            }
        }

        // =============================================
        // ENDPOINTS SUPPLÉMENTAIRES POUR ANALYSES
        // =============================================

        /// <summary>
        /// Récupère l'évolution des soldes sur une période donnée
        /// </summary>
        [HttpGet("evolution-soldes")]
        public async Task<IActionResult> GetEvolutionSoldes(
            [FromQuery] DateTime? dateDebut,
            [FromQuery] DateTime? dateFin,
            [FromQuery] string groupement = "jour") // jour, semaine, mois
        {
            try
            {
                dateDebut ??= DateTime.Now.AddDays(-30);
                dateFin ??= DateTime.Now;

                var mouvements = await _tresorerieService.GetMouvementsAsync(null, 0, null, null, dateDebut, dateFin);

                // Grouper les mouvements selon le type de groupement
                var evolutionSolde = mouvements
                    .GroupBy(m => groupement switch
                    {
                        "semaine" => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(m.DateMouvement, CalendarWeekRule.FirstDay, DayOfWeek.Monday).ToString(),
                        "mois" => m.DateMouvement.ToString("yyyy-MM"),
                        _ => m.DateMouvement.ToString("yyyy-MM-dd")
                    })
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        Periode = g.Key,
                        Entrees = g.Where(m => m.TypeMouvement == "Entree").Sum(m => m.Montant),
                        Sorties = g.Where(m => m.TypeMouvement == "Sortie").Sum(m => m.Montant),
                        SoldeNet = g.Where(m => m.TypeMouvement == "Entree").Sum(m => m.Montant) -
                                  g.Where(m => m.TypeMouvement == "Sortie").Sum(m => m.Montant),
                        NombreMouvements = g.Count()
                    })
                    .ToList();

                return Ok(new
                {
                    periode = new { dateDebut, dateFin },
                    groupement,
                    evolution = evolutionSolde,
                    resume = new
                    {
                        totalEntrees = evolutionSolde.Sum(e => e.Entrees),
                        totalSorties = evolutionSolde.Sum(e => e.Sorties),
                        soldeNetTotal = evolutionSolde.Sum(e => e.SoldeNet),
                        nombrePeriodes = evolutionSolde.Count
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les prévisions de trésorerie basées sur l'historique
        /// </summary>
        [HttpGet("previsions")]
        public async Task<IActionResult> GetPrevisionsTresorerie([FromQuery] int nbMoisPrevision = 3)
        {
            try
            {
                var stats = await _tresorerieService.GetStatsAsync();
                var mouvements = await _tresorerieService.GetMouvementsAsync(null, 90); // 3 derniers mois

                // Calcul des moyennes mensuelles
                var moyenneEntrees = mouvements.Where(m => m.TypeMouvement == "Entree").Average(m => m.Montant);
                var moyenneSorties = mouvements.Where(m => m.TypeMouvement == "Sortie").Average(m => m.Montant);

                var previsions = new List<object>();
                var soldeProjecte = stats.SoldeTotal;

                for (int i = 1; i <= nbMoisPrevision; i++)
                {
                    var datePrevision = DateTime.Now.AddMonths(i);
                    var entreePrevue = moyenneEntrees * 30; // Approximation mensuelle
                    var sortiePrevue = moyenneSorties * 30;
                    var soldeNetPrevu = entreePrevue - sortiePrevue;

                    soldeProjecte += soldeNetPrevu;

                    previsions.Add(new
                    {
                        Mois = datePrevision.ToString("yyyy-MM"),
                        EntreePrevue = entreePrevue,
                        SortiePrevue = sortiePrevue,
                        SoldeNetPrevu = soldeNetPrevu,
                        SoldeProjecte = soldeProjecte,
                        StatutPrevision = soldeProjecte > 0 ? "Positif" : "Négatif",
                        AlerteTresorerie = soldeProjecte < 1000 ? "Attention" : "Normal"
                    });
                }

                return Ok(new
                {
                    basePrevision = new
                    {
                        soldeActuel = stats.SoldeTotal,
                        moyenneEntreesMensuelle = moyenneEntrees * 30,
                        moyenneSortiesMensuelle = moyenneSorties * 30,
                        tendanceMensuelle = (moyenneEntrees - moyenneSorties) * 30
                    },
                    previsions,
                    alertes = previsions.Where(p => ((dynamic)p).AlerteTresorerie == "Attention").ToList(),
                    recommandations = new[]
                    {
                        soldeProjecte < 0 ? "Prévoir des financements ou réduire les dépenses" : null,
                        stats.Indicateurs.TauxCroissanceMensuel < 0 ? "Analyser la baisse des revenus" : null,
                        stats.SoldeTotal < 5000 ? "Constituer une réserve de sécurité" : null
                    }.Where(r => r != null)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les alertes de trésorerie automatiques
        /// </summary>
        [HttpGet("alertes")]
        public async Task<IActionResult> GetAlertesTresorerie()
        {
            try
            {
                var alertes = new List<object>();
                var comptes = await _tresorerieService.GetAllComptesAsync();
                var stats = await _tresorerieService.GetStatsAsync();

                // Alerte solde faible
                var comptesFaibles = comptes.Where(c => c.SoldeActuel < 500).ToList();
                if (comptesFaibles.Any())
                {
                    alertes.Add(new
                    {
                        Type = "SoldeFaible",
                        Severite = "Warning",
                        Message = $"{comptesFaibles.Count} compte(s) avec solde faible",
                        Details = comptesFaibles.Select(c => new { c.Nom, c.SoldeActuel }),
                        Couleur = "#f59e0b"
                    });
                }

                // Alerte découvert
                var comptesDecouvert = comptes.Where(c => c.SoldeActuel < 0).ToList();
                if (comptesDecouvert.Any())
                {
                    alertes.Add(new
                    {
                        Type = "Decouvert",
                        Severite = "Critical",
                        Message = $"{comptesDecouvert.Count} compte(s) en découvert",
                        Details = comptesDecouvert.Select(c => new { c.Nom, c.SoldeActuel }),
                        Couleur = "#dc2626"
                    });
                }

                // Alerte tendance négative
                if (stats.Indicateurs.TauxCroissanceMensuel < 0)
                {
                    alertes.Add(new
                    {
                        Type = "TendanceNegative",
                        Severite = "Warning",
                        Message = "Tendance mensuelle négative",
                        Details = $"Flux net mensuel: {stats.Indicateurs.TauxCroissanceMensuel:C}",
                        Couleur = "#f59e0b"
                    });
                }

                // Alerte trésorerie globale faible
                if (stats.SoldeTotal < 2000)
                {
                    alertes.Add(new
                    {
                        Type = "TresorerieFaible",
                        Severite = stats.SoldeTotal < 500 ? "Critical" : "Warning",
                        Message = "Trésorerie globale faible",
                        Details = $"Solde total: {stats.SoldeTotal:C}",
                        Couleur = stats.SoldeTotal < 500 ? "#dc2626" : "#f59e0b"
                    });
                }

                return Ok(new
                {
                    timestamp = DateTime.UtcNow,
                    nombreAlertes = alertes.Count,
                    alertesCritiques = alertes.Count(a => ((dynamic)a).Severite == "Critical"),
                    alertes,
                    recommandations = new[]
                    {
                        "Surveiller régulièrement les soldes",
                        "Prévoir des lignes de crédit en cas de besoin",
                        "Optimiser les délais de paiement clients",
                        "Négocier les délais fournisseurs"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // MÉTHODES PRIVÉES UTILITAIRES
        // =============================================

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 3; // Fallback pour développement
        }

        private static string GetStatutSolde(decimal solde, string typeCompte)
        {
            return typeCompte switch
            {
                "Courant" when solde < 0 => "Découvert",
                "Courant" when solde < 500 => "Faible",
                "Courant" when solde < 5000 => "Moyen",
                "Courant" => "Élevé",
                "Epargne" when solde < 1000 => "Faible",
                "Epargne" when solde < 10000 => "Moyen",
                "Epargne" => "Élevé",
                "Caisse" when solde < 0 => "Déficit",
                "Caisse" when solde < 100 => "Faible",
                "Caisse" => "Normal",
                _ => "Normal"
            };
        }

        private static string GetCouleurSolde(decimal solde)
        {
            return solde switch
            {
                < 0 => "#dc2626", // Rouge pour découvert
                < 500 => "#f59e0b", // Orange pour faible
                < 5000 => "#3b82f6", // Bleu pour moyen
                _ => "#10b981" // Vert pour élevé
            };
        }

        private static string GetCouleurTypeMouvement(string typeMouvement)
        {
            return typeMouvement switch
            {
                "Entree" => "#10b981", // Vert
                "Sortie" => "#ef4444", // Rouge
                "Virement" => "#3b82f6", // Bleu
                _ => "#6b7280" // Gris par défaut
            };
        }
    }
}