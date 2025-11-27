using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.Factures
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FacturesController : BaseController
    {
        private readonly FactureService _factureService;

        public FacturesController(FactureService factureService)
        {
            _factureService = factureService;
        }

        /// <summary>
        /// Récupère toutes les factures
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? statut = null, [FromQuery] string? typeFacture = null)
        {
            try
            {
                var factures = await _factureService.GetAllAsync();

                // Filtrage optionnel
                if (!string.IsNullOrEmpty(statut))
                {
                    factures = factures.Where(f => f.Statut.Equals(statut, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (!string.IsNullOrEmpty(typeFacture))
                {
                    factures = factures.Where(f => f.TypeFacture.Equals(typeFacture, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var result = factures.Select(f => new
                {
                    f.Id,
                    f.Numero,
                    f.TypeFacture,
                    f.Titre,
                    f.Statut,
                    f.MontantHT,
                    f.MontantTVA,
                    f.MontantTTC,
                    f.MontantPaye,
                    MontantRestant = f.MontantTTC - f.MontantPaye,
                    f.DateFacture,
                    f.DateEcheance,
                    f.DateCreation,
                    f.ConditionsPaiement,
                    // Débiteur (Client ou Sous-traitant)
                    Debiteur = f.ClientId.HasValue ? f.Client?.Nom : null,
                    DebiteurType = f.ClientId.HasValue ? "Client" : "Sous-traitant",
                    // Relations
                    f.DevisId,
                    f.ProjetId,
                    // Indicateurs
                    EstEnRetard = f.Statut == "Envoyee" && f.DateEcheance < DateTime.Now,
                    JoursRetard = f.Statut == "Envoyee" && f.DateEcheance < DateTime.Now ?
                        (DateTime.Now - f.DateEcheance).Days : 0,
                    DetailDebiteur = f.Client != null
                    ? new
                    {
                        f.Client.Id,
                        nom = !string.IsNullOrEmpty(f.Client.Nom) ? $"{f.Client.Nom}".Trim() : "",
                        ncc = !string.IsNullOrEmpty(f.Client.Ncc) ? $"{f.Client.Ncc}".Trim() : "",
                        raisonSociale = !string.IsNullOrEmpty(f.Client.RaisonSociale) ? $"{f.Client.RaisonSociale}".Trim() : "",
                        email = !string.IsNullOrEmpty(f.Client.Email) ? $"{f.Client.Email}".Trim() : "",
                        telephone = !string.IsNullOrEmpty(f.Client.Telephone) ? $"{f.Client.Telephone}".Trim() : "",
                        adresse = !string.IsNullOrEmpty(f.Client.Adresse) ? $"{f.Client.Adresse}".Trim() : "",

                    } : f.SousTraitant != null
                   ? new
                   {
                       f.SousTraitant.Id,
                       nom = !string.IsNullOrEmpty(f.SousTraitant.Nom) ? $"{f.SousTraitant.Nom}".Trim() : "",
                       ncc = !string.IsNullOrEmpty(f.SousTraitant.Ncc) ? $"{f.SousTraitant.Ncc}".Trim() : "",
                       raisonSociale = !string.IsNullOrEmpty(f.SousTraitant.RaisonSociale) ? $"{f.SousTraitant.RaisonSociale}".Trim() : "",
                       email = !string.IsNullOrEmpty(f.SousTraitant.Email) ? $"{f.SousTraitant.Email}".Trim() : "",
                       telephone = !string.IsNullOrEmpty(f.SousTraitant.Telephone) ? $"{f.SousTraitant.Telephone}".Trim() : "",
                       adresse = !string.IsNullOrEmpty(f.SousTraitant.Adresse) ? $"{f.SousTraitant.Adresse}".Trim() : "",
                   } : null
                }).OrderByDescending(f => f.DateCreation);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }


        /// <summary>
        /// Récupère une facture par son ID avec détails complets
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var facture = await _factureService.GetByIdAsync(id);
                if (facture == null) return NotFound(new { message = "Facture non trouvée" });

                var result = new
                {
                    facture.Id,
                    facture.Numero,
                    facture.TypeFacture,
                    facture.Titre,
                    facture.Description,
                    facture.Statut,
                    facture.MontantHT,
                    facture.MontantTVA,
                    facture.TauxTVA,
                    facture.MontantTTC,
                    facture.MontantPaye,
                    MontantRestant = facture.MontantTTC - facture.MontantPaye,
                    facture.DateFacture,
                    facture.DateEcheance,
                    facture.DateEnvoi,
                    facture.DatePaiement,
                    facture.DateCreation,
                    facture.ConditionsPaiement,
                    facture.ModePaiement,
                    facture.ReferenceClient,
                    // Relations
                    Client = facture.Client != null ? new
                    {
                        facture.Client.Id,
                        facture.Client.Nom,
                        facture.Client.RaisonSociale,
                        facture.Client.Email,
                        facture.Client.Telephone
                    } : null,
                    SousTraitant = facture.SousTraitant != null ? new
                    {
                        facture.SousTraitant.Id,
                        facture.SousTraitant.Nom,
                        facture.SousTraitant.RaisonSociale,
                        facture.SousTraitant.Email,
                        facture.SousTraitant.Telephone
                    } : null,

                    // Débiteur (Client ou Sous-traitant)
                    Debiteur = facture.ClientId.HasValue ? facture.Client?.Nom : null,
                    DebiteurType = facture.ClientId.HasValue ? "Client" : "Sous-traitant",
                    DetailDebiteur = facture.Client != null
                    ? new
                    {
                        facture.Client.Id,
                        nom = !string.IsNullOrEmpty(facture.Client.Nom) ? $"{facture.Client.Nom}".Trim() : "",
                        ncc = !string.IsNullOrEmpty(facture.Client.Ncc) ? $"{facture.Client.Ncc}".Trim() : "",
                        raisonSociale = !string.IsNullOrEmpty(facture.Client.RaisonSociale) ? $"{facture.Client.RaisonSociale}".Trim() : "",
                        email = !string.IsNullOrEmpty(facture.Client.Email) ? $"{facture.Client.Email}".Trim() : "",
                        telephone = !string.IsNullOrEmpty(facture.Client.Telephone) ? $"{facture.Client.Telephone}".Trim() : "",
                        adresse = !string.IsNullOrEmpty(facture.Client.Adresse) ? $"{facture.Client.Adresse}".Trim() : "",

                    } : facture.SousTraitant != null
                   ? new
                   {
                       facture.SousTraitant.Id,
                       nom = !string.IsNullOrEmpty(facture.SousTraitant.Nom) ? $"{facture.SousTraitant.Nom}".Trim() : "",
                       ncc = !string.IsNullOrEmpty(facture.SousTraitant.Ncc) ? $"{facture.SousTraitant.Ncc}".Trim() : "",
                       raisonSociale = !string.IsNullOrEmpty(facture.SousTraitant.RaisonSociale) ? $"{facture.SousTraitant.RaisonSociale}".Trim() : "",
                       email = !string.IsNullOrEmpty(facture.SousTraitant.Email) ? $"{facture.SousTraitant.Email}".Trim() : "",
                       telephone = !string.IsNullOrEmpty(facture.SousTraitant.Telephone) ? $"{facture.SousTraitant.Telephone}".Trim() : "",
                       adresse = !string.IsNullOrEmpty(facture.SousTraitant.Adresse) ? $"{facture.SousTraitant.Adresse}".Trim() : "",
                   } : null,

                    facture.DevisId,
                    facture.ProjetId,
                    // Lignes et échéanciers
                    Lignes = facture.Lignes?.Select(l => new
                    {
                        l.Id,
                        l.Ordre,
                        l.Designation,
                        l.Description,
                        l.Quantite,
                        l.Unite,
                        l.PrixUnitaireHT,
                        TotalHT = l.Quantite * l.PrixUnitaireHT
                    }).OrderBy(l => l.Ordre),
                    Echeanciers = facture.Echeanciers?.Select(e => new
                    {
                        e.Id,
                        e.Ordre,
                        e.Description,
                        e.MontantTTC,
                        e.DateEcheance,
                        e.Statut,
                        e.DatePaiement,
                        e.ModePaiement,
                        e.ReferencePaiement
                    }).OrderBy(e => e.Ordre)
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Crée une nouvelle facture
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFactureRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var utilisateurId = GetCurrentUserId();
                var facture = new Facture
                {
                    TypeFacture = model.TypeFacture,
                    ClientId = model.ClientId,
                    SousTraitantId = model.SousTraitantId,
                    DevisId = model.DevisId,
                    ProjetId = model.ProjetId,
                    Titre = model.Titre,
                    Description = model.Description,
                    Statut = "Brouillon",
                    DateCreation = DateTime.UtcNow,
                    DateFacture = model.DateFacture,
                    DateEcheance = model.DateEcheance,
                    DateModification = DateTime.UtcNow,
                    ConditionsPaiement = model.ConditionsPaiement ?? "30 jours",
                    ReferenceClient = model.ReferenceClient,
                    TauxTVA = 18,
                    UtilisateurCreation = utilisateurId
                };

                // Mapper les lignes
                if (model.Lignes != null && model.Lignes.Any())
                {
                    facture.Lignes = model.Lignes.Select(l => new LigneFacture
                    {
                        Designation = l.Designation,
                        Description = l.Description,
                        Quantite = l.Quantite,
                        Unite = l.Unite,
                        PrixUnitaireHT = l.PrixUnitaireHT
                    }).ToList();

                    // Calculer les montants
                    facture.MontantHT = facture.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);
                    facture.MontantTVA = facture.MontantHT * facture.TauxTVA / 100;
                    facture.MontantTTC = facture.MontantHT + facture.MontantTVA;
                }

                // Mapper les échéanciers
                if (model.Echeanciers != null && model.Echeanciers.Any())
                {
                    facture.Echeanciers = model.Echeanciers.Select(e => new Echeancier
                    {
                        Description = e.Description,
                        MontantTTC = e.MontantTTC,
                        DateEcheance = e.DateEcheance,
                        Statut = "EnAttente"
                    }).ToList();

                    // Vérifier que la somme des échéanciers = montant TTC
                    var totalEcheanciers = facture.Echeanciers.Sum(e => e.MontantTTC);
                    if (Math.Abs(totalEcheanciers - facture.MontantTTC) > 0.01m)
                    {
                        return BadRequest(new { message = "La somme des échéanciers doit égaler le montant TTC de la facture" });
                    }
                }

                var factureId = await _factureService.CreateAsync(facture);
                facture.Id = factureId;

                return CreatedAtAction(nameof(Get), new { id = factureId }, new
                {
                    message = "Facture créée avec succès",
                    id = factureId,
                    numero = facture.Numero
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Crée une facture à partir d'un devis validé
        /// </summary>
        [HttpPost("from-devis/{devisId}")]
        public async Task<IActionResult> CreateFromDevis(int devisId)
        {
            try
            {
                var facture = await _factureService.CreateFromDevisAsync(devisId);
                if (facture == null)
                    return BadRequest(new { message = "Impossible de créer la facture depuis ce devis" });

                return CreatedAtAction(nameof(Get), new { id = facture.Id }, new
                {
                    message = "Facture créée depuis le devis avec succès",
                    id = facture.Id,
                    numero = facture.Numero
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Met à jour une facture (seulement si statut = Brouillon)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateFactureRequest model)
        {
            try
            {
                var existing = await _factureService.GetByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Facture non trouvée" });

                if (existing.Statut != "Brouillon")
                {
                    return BadRequest(new { message = "Seules les factures en brouillon peuvent être modifiées" });
                }

                // Mise à jour des propriétés
                existing.Titre = model.Titre;
                existing.Description = model.Description;
                existing.DateFacture = model.DateFacture;
                existing.DateEcheance = model.DateEcheance;
                existing.ConditionsPaiement = model.ConditionsPaiement ?? "30 jours";
                existing.ReferenceClient = model.ReferenceClient;
                existing.Statut = model.Statut;
                existing.DateModification = DateTime.UtcNow;

                // Recalculer les montants si lignes modifiées
                if (model.Lignes != null && model.Lignes.Any())
                {
                    existing.Lignes = model.Lignes.Select(l => new LigneFacture
                    {
                        Designation = l.Designation,
                        Description = l.Description,
                        Quantite = l.Quantite,
                        Unite = l.Unite,
                        PrixUnitaireHT = l.PrixUnitaireHT
                    }).ToList();

                    existing.MontantHT = existing.Lignes.Sum(l => l.Quantite * l.PrixUnitaireHT);
                    existing.MontantTVA = existing.MontantHT * existing.TauxTVA / 100;
                    existing.MontantTTC = existing.MontantHT + existing.MontantTVA;
                }

                await _factureService.UpdateAsync(existing);
                return Ok(new { message = "Facture modifiée avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Envoie une facture (change le statut à Envoyee)
        /// </summary>
        [HttpPost("{id}/envoyer")]
        public async Task<IActionResult> Envoyer(int id)
        {
            try
            {
                var existing = await _factureService.GetByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Facture non trouvée" });

                if (existing.Statut != "Brouillon")
                {
                    return BadRequest(new { message = "Seules les factures en brouillon peuvent être envoyées" });
                }

                await _factureService.UpdateStatutAsync(id, "Envoyee");

                // TODO: Implémenter l'envoi d'email avec PDF
                // await _emailService.SendFactureAsync(existing);

                return Ok(new { message = "Facture envoyée avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Marque une facture comme payée (partiellement ou totalement)
        /// </summary>
        [HttpPost("{id}/marquer-payee")]
        public async Task<IActionResult> MarquerPayee(int id, [FromBody] MarquerPayeRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existing = await _factureService.GetByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Facture non trouvée" });

                if (existing.Statut == "Annulee")
                {
                    return BadRequest(new { message = "Impossible de marquer comme payée une facture annulée" });
                }

                if (model.MontantPaye <= 0)
                {
                    return BadRequest(new { message = "Le montant payé doit être supérieur à 0" });
                }

                if ((existing.MontantPaye + model.MontantPaye) > existing.MontantTTC)
                {
                    return BadRequest(new { message = "Le montant total payé ne peut pas dépasser le montant de la facture" });
                }

                // Ajuster le montant payé (cumulatif)
                var nouveauMontantPaye = existing.MontantPaye + model.MontantPaye;
                var requestAjuste = new MarquerPayeRequest
                {
                    MontantPaye = nouveauMontantPaye,
                    ModePaiement = model.ModePaiement,
                    ReferencePaiement = model.ReferencePaiement,
                    DatePaiement = model.DatePaiement
                };

                await _factureService.MarquerPayeAsync(id, requestAjuste);

                var nouveauStatut = nouveauMontantPaye >= existing.MontantTTC ? "totalement" : "partiellement";
                return Ok(new
                {
                    message = $"Paiement de {model.MontantPaye:C} enregistré avec succès. Facture {nouveauStatut} payée.",
                    montantPaye = model.MontantPaye,
                    montantTotalPaye = nouveauMontantPaye,
                    montantRestant = existing.MontantTTC - nouveauMontantPaye
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Annule une facture
        /// </summary>
        [HttpPost("{id}/annuler")]
        public async Task<IActionResult> Annuler(int id, [FromBody] AnnulerFactureRequest? model = null)
        {
            try
            {
                var existing = await _factureService.GetByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Facture non trouvée" });

                if (existing.Statut == "Payee")
                {
                    return BadRequest(new { message = "Impossible d'annuler une facture déjà payée" });
                }

                await _factureService.UpdateStatutAsync(id, "Annulee");
                return Ok(new
                {
                    message = "Facture annulée avec succès",
                    motif = model?.Motif
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les factures impayées avec calcul des retards
        /// </summary>
        [HttpGet("impayes")]
        public async Task<IActionResult> GetFacturesImpayes()
        {
            try
            {
                var factures = await _factureService.GetAllAsync();
                var impayes = factures.Where(f => f.Statut == "Envoyee" && (f.MontantTTC - f.MontantPaye) > 0)
                                     .Select(f => new
                                     {
                                         f.Id,
                                         f.Numero,
                                         f.Titre,
                                         f.MontantTTC,
                                         f.MontantPaye,
                                         MontantRestant = f.MontantTTC - f.MontantPaye,
                                         f.DateEcheance,
                                         JoursRetard = f.DateEcheance < DateTime.Now ? (DateTime.Now - f.DateEcheance).Days : 0,
                                         Debiteur = f.ClientId.HasValue ?
                                             (!string.IsNullOrEmpty(f.Client?.RaisonSociale) ? f.Client.RaisonSociale :
                                              $"{f.Client?.Nom}".Trim()) :
                                             f.SousTraitant?.Nom,
                                         Email = f.ClientId.HasValue ? f.Client?.Email : f.SousTraitant?.Email,
                                         Telephone = f.ClientId.HasValue ? f.Client?.Telephone : f.SousTraitant?.Telephone
                                     })
                                     .OrderByDescending(f => f.JoursRetard)
                                     .ThenByDescending(f => f.MontantRestant);

                return Ok(impayes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Génère et télécharge le PDF d'une facture
        /// </summary>
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GetPDF(int id)
        {
            try
            {
                var facture = await _factureService.GetByIdAsync(id);
                if (facture == null) return NotFound(new { message = "Facture non trouvée" });

                // TODO: Implémenter la génération PDF
                // var pdfBytes = await _pdfService.GenerateFacturePDF(facture);
                // return File(pdfBytes, "application/pdf", $"Facture_{facture.Numero}.pdf");

                return Ok(new { message = "Génération PDF à implémenter", factureNumero = facture.Numero });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les statistiques des factures
        /// </summary>
        [HttpGet("statistiques")]
        public async Task<IActionResult> GetStatistiques([FromQuery] int? annee = null)
        {
            try
            {
                var factures = await _factureService.GetAllAsync();
                var anneeFiltre = annee ?? DateTime.Now.Year;

                var facturesAnnee = factures.Where(f => f.DateFacture.Year == anneeFiltre);

                var stats = new
                {
                    // Golbal 
                    totalFacturesGolbal = factures.Count(),
                    retardPayementGolbal = factures.Where(x => x.DateEcheance > DateTime.Now).Count(),
                    montantTotalPayeGolbal = factures.Sum(f => f.MontantPaye),
                    montantRestantARecouvrerGolbal = factures.Sum(f => f.MontantTTC - f.MontantPaye),

                    //Annee

                    Annee = anneeFiltre,
                    totalFactures = facturesAnnee.Count(),
                    montantTotalFacture = facturesAnnee.Sum(f => f.MontantTTC),
                    montantTotalPaye = facturesAnnee.Sum(f => f.MontantPaye),
                    montantRestantARecouvrer = facturesAnnee.Sum(f => f.MontantTTC - f.MontantPaye),


                    // Répartition par statut
                    repartitionStatuts = facturesAnnee.GroupBy(f => f.Statut)
                                                     .Select(g => new { Statut = g.Key, Nombre = g.Count(), Montant = g.Sum(f => f.MontantTTC) }),

                    // Répartition par type
                    repartitionTypes = facturesAnnee.GroupBy(f => f.TypeFacture)
                                                   .Select(g => new { Type = g.Key, Nombre = g.Count(), Montant = g.Sum(f => f.MontantTTC) }),

                    // Évolution mensuelle
                    evolutionMensuelle = facturesAnnee.GroupBy(f => f.DateFacture.Month)
                                                     .Select(g => new
                                                     {
                                                         Mois = g.Key,
                                                         NomMois = new DateTime(anneeFiltre, g.Key, 1).ToString("MMMM"),
                                                         Nombre = g.Count(),
                                                         Montant = g.Sum(f => f.MontantTTC)
                                                     })
                                                     .OrderBy(x => x.Mois)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 3; // Fallback pour développement
        }
    }
    public class AnnulerFactureRequest
    {
        public string? Motif { get; set; }
    }
}