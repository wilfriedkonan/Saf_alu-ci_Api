using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.SousTraitants
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize]
    public class SousTraitantsController : ControllerBase
    {
        private readonly SousTraitantService _sousTraitantService;

        public SousTraitantsController(SousTraitantService sousTraitantService)
        {
            _sousTraitantService = sousTraitantService;
        }

        /// <summary>
        /// Récupère tous les sous-traitants avec filtrage optionnel
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? specialite = null, [FromQuery] bool? assuranceValide = null, [FromQuery] decimal? noteMin = null)
        {
            try
            {
                var sousTraitants = await _sousTraitantService.GetAllAsync();

                // Filtrage par spécialité
                if (!string.IsNullOrEmpty(specialite))
                {
                    sousTraitants = sousTraitants.Where(st =>
                        st.Specialites?.Any(s => s.Specialite?.Nom.Contains(specialite, StringComparison.OrdinalIgnoreCase) == true) == true
                    ).ToList();
                }

                // Filtrage par assurance
                //if (assuranceValide.HasValue)
                //{
                //    sousTraitants = sousTraitants.Where(st => st.AssuranceValide == assuranceValide.Value).ToList();
                //}

                // Filtrage par note minimum
                if (noteMin.HasValue)
                {
                    sousTraitants = sousTraitants.Where(st => st.NoteMoyenne >= noteMin.Value).ToList();
                }

                var result = sousTraitants.Select(st => new
                {
                    st.Id,
                    st.Nom,
                    st.RaisonSociale,
                    st.Email,
                    st.Telephone,
                    st.Ville,
                    st.Adresse,
                    st.NoteMoyenne,
                    st.NombreEvaluations,
                    st.Actif,
                    // Contact principal
                    Contact = new
                    {
                        Nom = !string.IsNullOrEmpty(st.NomContact) ? $"{st.NomContact}"
                            : null,
                        st.EmailContact,
                        st.TelephoneContact
                    },

                    // Assurance
                    //Assurance = new
                    //{
                    //    st.AssuranceValide,
                    //    st.DateExpirationAssurance,
                    //    st.NumeroAssurance,
                    //    ExpirationProche = st.DateExpirationAssurance.HasValue &&
                    //                      st.DateExpirationAssurance.Value <= DateTime.Now.AddDays(30)
                    //},

                    // Spécialités
                    Specialites = st.Specialites?.Select(s => new
                    {
                        s.SpecialiteId,
                        Nom = s.Specialite?.Nom,
                        Description = s.Specialite?.Description,
                        s.NiveauExpertise,
                        NiveauLabel = GetNiveauExpertiseLabel(s.NiveauExpertise),
                        Couleur = s.Specialite?.Couleur
                    }).ToList(),

                    st.DateCreation,

                    // Indicateurs
                    //StatutAssurance = GetStatutAssurance(st),
                    StatutNote = GetStatutNote(st.NoteMoyenne, st.NombreEvaluations)
                }).OrderByDescending(st => st.NoteMoyenne)
                  .ThenBy(st => st.Nom);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère un sous-traitant par son ID avec historique complet
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            try
            {
                var sousTraitant = await _sousTraitantService.GetByIdAsync(id);
                if (sousTraitant == null) return NotFound(new { message = "Sous-traitant non trouvé" });

                var result = new
                {
                    // Informations principales
                    sousTraitant.Id,
                    sousTraitant.Nom,
                    sousTraitant.RaisonSociale,
                    sousTraitant.Email,
                    sousTraitant.Telephone,
                    sousTraitant.Adresse,
                    sousTraitant.Ville,
                    sousTraitant.Ncc,

                    // Contact principal
                    Contact = new
                    {
                        sousTraitant.NomContact,
                        sousTraitant.EmailContact,
                        sousTraitant.TelephoneContact
                    },

                    // Évaluations
                    Evaluation = new
                    {
                        sousTraitant.NoteMoyenne,
                        sousTraitant.NombreEvaluations,
                        StatutNote = GetStatutNote(sousTraitant.NoteMoyenne, sousTraitant.NombreEvaluations)
                    },

                    // Assurance et certifications
                    //Assurance = new
                    //{
                    //    sousTraitant.AssuranceValide,
                    //    sousTraitant.DateExpirationAssurance,
                    //    sousTraitant.NumeroAssurance,
                    //    sousTraitant.Certifications,
                    //    //StatutAssurance = GetStatutAssurance(sousTraitant)
                    //},

                    // Spécialités avec niveau d'expertise
                    Specialites = sousTraitant.Specialites?.Select(s => new
                    {
                        s.SpecialiteId,
                        Nom = s.Specialite?.Nom,
                        Description = s.Specialite?.Description,
                        s.NiveauExpertise,
                        NiveauLabel = GetNiveauExpertiseLabel(s.NiveauExpertise),
                        Couleur = s.Specialite?.Couleur
                    }).ToList(),

                    // Évaluations récentes
                    EvaluationsRecentes = sousTraitant.Evaluations?.OrderByDescending(e => e.DateEvaluation)
                                                                  .Take(10)
                                                                  .Select(e => new
                                                                  {
                                                                      e.Id,
                                                                      e.Note,
                                                                      e.Commentaire,
                                                                      e.DateEvaluation,
                                                                      e.ProjetId,
                                                                      e.EtapeProjetId,
                                                                      // TODO: Ajouter nom projet depuis ProjetService si nécessaire
                                                                  }).ToList(),

                    sousTraitant.DateCreation,
                    sousTraitant.DateModification
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère toutes les spécialités disponibles
        /// </summary>
        [HttpGet("specialites")]
        public async Task<IActionResult> GetSpecialites()
        {
            try
            {
                var specialites = await _sousTraitantService.GetAllSpecialitesAsync();
                return Ok(specialites.Select(s => new
                {
                    s.Id,
                    s.Nom,
                    s.Description,
                    s.Couleur,
                    s.Actif
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Crée un nouveau sous-traitant
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSousTraitantRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var utilisateurId = GetCurrentUserId();
                var sousTraitant = new SousTraitant
                {
                    Nom = model.Nom,
                    RaisonSociale = model.RaisonSociale,
                    Email = model.Email,
                    Telephone = model.Telephone,
                    Adresse = model.Adresse,
                    Ville = model.Ville,
                    Ncc = model.Ncc,
                    NomContact = model.NomContact,
                    EmailContact = model.EmailContact,
                    TelephoneContact = model.TelephoneContact,
                    NoteMoyenne = 0,
                    NombreEvaluations = 0,
                    //AssuranceValide = model.AssuranceValide,
                    //DateExpirationAssurance = model.DateExpirationAssurance,
                    //NumeroAssurance = model.NumeroAssurance,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    Actif = true,
                    UtilisateurCreation = utilisateurId
                };

                // Mapper les spécialités
                if (model.SpecialiteIds != null && model.SpecialiteIds.Any())
                {
                    sousTraitant.Specialites = model.SpecialiteIds.Select(id => new SousTraitantSpecialite
                    {
                        SpecialiteId = id,
                        NiveauExpertise = 3 // Niveau par défaut
                    }).ToList();
                }

                var sousTraitantId = await _sousTraitantService.CreateAsync(sousTraitant);
                sousTraitant.Id = sousTraitantId;

                return CreatedAtAction(nameof(Get), new { id = sousTraitantId }, new
                {
                    message = "Sous-traitant créé avec succès",
                    id = sousTraitantId,
                    nom = sousTraitant.Nom
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Met à jour un sous-traitant
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSousTraitantRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existing = await _sousTraitantService.GetByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Sous-traitant non trouvé" });

                // Mise à jour des propriétés
                existing.Nom = model.Nom;
                existing.RaisonSociale = model.RaisonSociale;
                existing.Email = model.Email;
                existing.Telephone = model.Telephone;
                existing.Adresse = model.Adresse;
                existing.Ville = model.Ville;
                existing.Ncc = model.Ncc;
                existing.NomContact = model.NomContact;
                existing.EmailContact = model.EmailContact;
                existing.TelephoneContact = model.TelephoneContact;
                //existing.AssuranceValide = model.AssuranceValide ?? existing.AssuranceValide;
                //existing.DateExpirationAssurance = model.DateExpirationAssurance;
                //existing.NumeroAssurance = model.NumeroAssurance;
                existing.Certifications = model.Certifications;
                existing.DateModification = DateTime.UtcNow;

                // Mapper les spécialités avec niveaux d'expertise
                if (model.Specialites != null && model.Specialites.Any())
                {
                    existing.Specialites = model.Specialites.Select(s => new SousTraitantSpecialite
                    {
                        SousTraitantId = existing.Id,
                        SpecialiteId = s.SpecialiteId,
                        NiveauExpertise = s.NiveauExpertise
                    }).ToList();
                }

                await _sousTraitantService.UpdateAsync(existing);
                return Ok(new { message = "Sous-traitant modifié avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Supprime (désactive) un sous-traitant
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var existing = await _sousTraitantService.GetByIdAsync(id);
                if (existing == null) return NotFound(new { message = "Sous-traitant non trouvé" });

                await _sousTraitantService.DeleteAsync(id);
                return Ok(new { message = "Sous-traitant supprimé avec succès" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Crée une évaluation pour un sous-traitant
        /// </summary>
        [HttpPost("{id}/evaluations")]
        public async Task<IActionResult> CreateEvaluation(int id, [FromBody] CreateEvaluationRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (model.Note < 1 || model.Note > 5)
                {
                    return BadRequest(new { message = "La note doit être comprise entre 1 et 5" });
                }

                // Vérifier que le sous-traitant existe
                var sousTraitant = await _sousTraitantService.GetByIdAsync(id);
                if (sousTraitant == null) return NotFound(new { message = "Sous-traitant non trouvé" });

                var utilisateurId = GetCurrentUserId();
                var evaluation = new EvaluationSousTraitant
                {
                    SousTraitantId = id,
                    ProjetId = model.ProjetId,
                    EtapeProjetId = model.EtapeProjetId,
                    Note = model.Note,
                    Commentaire = model.Commentaire,
                    Criteres = model.Criteres != null ? System.Text.Json.JsonSerializer.Serialize(model.Criteres) : null,
                    DateEvaluation = DateTime.UtcNow,
                    EvaluateurId = utilisateurId
                };

                var evaluationId = await _sousTraitantService.CreateEvaluationAsync(evaluation);
                evaluation.Id = evaluationId;

                return Ok(new
                {
                    message = "Évaluation enregistrée avec succès",
                    id = evaluationId,
                    note = evaluation.Note,
                    nouvelleMoyenne = "Calculée automatiquement" // La procédure stockée recalcule
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère toutes les évaluations d'un sous-traitant
        /// </summary>
        [HttpGet("{id}/evaluations")]
        public async Task<IActionResult> GetEvaluations(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                // TODO: Implémenter GetEvaluationsBySousTraitantAsync dans le service
                // Pour l'instant, récupération via GetByIdAsync
                var sousTraitant = await _sousTraitantService.GetByIdAsync(id);
                if (sousTraitant == null) return NotFound(new { message = "Sous-traitant non trouvé" });

                var evaluations = sousTraitant.Evaluations?.Skip((page - 1) * pageSize)
                                                         .Take(pageSize)
                                                         .OrderByDescending(e => e.DateEvaluation)
                                                         .Select(e => new
                                                         {
                                                             e.Id,
                                                             e.Note,
                                                             e.Commentaire,
                                                             e.DateEvaluation,
                                                             e.ProjetId,
                                                             e.EtapeProjetId,
                                                             Criteres = !string.IsNullOrEmpty(e.Criteres)
                                                                 ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(e.Criteres)
                                                                 : null
                                                         }).ToList();

                return Ok(new
                {
                    evaluations,
                    totalEvaluations = sousTraitant.NombreEvaluations,
                    noteMoyenne = sousTraitant.NoteMoyenne,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Récupère les sous-traitants recommandés pour une spécialité donnée
        /// </summary>
        [HttpGet("recommandations")]
        public async Task<IActionResult> GetRecommandations([FromQuery] int? specialiteId, [FromQuery] decimal noteMin = 3.0m)
        {
            try
            {
                var sousTraitants = await _sousTraitantService.GetAllAsync();

                var recommandations = sousTraitants.Where(st =>
                    st.NoteMoyenne >= noteMin &&
                    //st.AssuranceValide &&
                    (!specialiteId.HasValue || st.Specialites?.Any(s => s.SpecialiteId == specialiteId.Value) == true))
                    .OrderByDescending(st => st.NoteMoyenne)
                    .ThenByDescending(st => st.NombreEvaluations)
                    .Select(st => new
                    {
                        st.Id,
                        st.Nom,
                        st.NoteMoyenne,
                        st.NombreEvaluations,
                        st.Telephone,
                        st.Email,
                        Specialites = st.Specialites?.Where(s => !specialiteId.HasValue || s.SpecialiteId == specialiteId.Value)
                                                    .Select(s => new { s.Specialite?.Nom, s.NiveauExpertise })
                                                    .ToList(),
                        ScoreRecommandation = CalculerScoreRecommandation(st)
                    })
                    .Take(10);

                return Ok(recommandations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// Met à jour le statut d'assurance d'un sous-traitant
        /// </summary>
        //[HttpPost("{id}/assurance")]
        //public async Task<IActionResult> UpdateAssurance(int id, [FromBody] UpdateAssuranceRequest model)
        //{
        //    try
        //    {
        //        if (!ModelState.IsValid)
        //            return BadRequest(ModelState);

        //        var existing = await _sousTraitantService.GetByIdAsync(id);
        //        if (existing == null) return NotFound(new { message = "Sous-traitant non trouvé" });

        //        existing.AssuranceValide = model.AssuranceValide;
        //        existing.DateExpirationAssurance = model.DateExpirationAssurance;
        //        existing.NumeroAssurance = model.NumeroAssurance;
        //        existing.DateModification = DateTime.UtcNow;

        //        await _sousTraitantService.UpdateAsync(existing);

        //        return Ok(new { message = "Informations d'assurance mises à jour avec succès" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
        //    }
        //}

        /// <summary>
        /// Récupère les statistiques des sous-traitants
        /// </summary>
        [HttpGet("statistiques")]
        public async Task<IActionResult> GetStatistiques()
        {
            try
            {
                var sousTraitants = await _sousTraitantService.GetAllAsync();

                var stats = new
                {
                    TotalSousTraitants = sousTraitants.Count,
                    //AvecAssuranceValide = sousTraitants.Count(st => st.AssuranceValide),
                    //AssuranceExpirantSous30Jours = sousTraitants.Count(st =>
                    //    st.DateExpirationAssurance.HasValue &&
                    //    st.DateExpirationAssurance.Value <= DateTime.Now.AddDays(30)),

                    NoteMoyenneGlobale = sousTraitants.Where(st => st.NombreEvaluations > 0).Average(st => (double)st.NoteMoyenne),
                    TotalEvaluations = sousTraitants.Sum(st => st.NombreEvaluations),

                    RepartitionNotes = new
                    {
                        Excellent = sousTraitants.Count(st => st.NoteMoyenne >= 4.5m),
                        TresBien = sousTraitants.Count(st => st.NoteMoyenne >= 4.0m && st.NoteMoyenne < 4.5m),
                        Bien = sousTraitants.Count(st => st.NoteMoyenne >= 3.0m && st.NoteMoyenne < 4.0m),
                        Passable = sousTraitants.Count(st => st.NoteMoyenne >= 2.0m && st.NoteMoyenne < 3.0m),
                        Insuffisant = sousTraitants.Count(st => st.NoteMoyenne > 0 && st.NoteMoyenne < 2.0m),
                        NonEvalue = sousTraitants.Count(st => st.NombreEvaluations == 0)
                    },

                    TopSpecialites = await GetTopSpecialites(),

                    RepartitionVilles = sousTraitants.Where(st => !string.IsNullOrEmpty(st.Ville))
                                                    .GroupBy(st => st.Ville)
                                                    .Select(g => new { Ville = g.Key, Nombre = g.Count() })
                                                    .OrderByDescending(x => x.Nombre)
                                                    .Take(10)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // MÉTHODES PRIVÉES
        // =============================================

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 3;
        }

        //private static string GetStatutAssurance(SousTraitant sousTraitant)
        //{
        //    if (!sousTraitant.AssuranceValide) return "Non valide";
        //    if (!sousTraitant.DateExpirationAssurance.HasValue) return "Valide";

        //    var joursRestants = (sousTraitant.DateExpirationAssurance.Value - DateTime.Now).Days;
        //    return joursRestants switch
        //    {
        //        < 0 => "Expirée",
        //        <= 30 => "Expire bientôt",
        //        _ => "Valide"
        //    };
        //}

        private static string GetStatutNote(decimal noteMoyenne, int nombreEvaluations)
        {
            if (nombreEvaluations == 0) return "Non évalué";
            return noteMoyenne switch
            {
                >= 4.5m => "Excellent",
                >= 4.0m => "Très bien",
                >= 3.0m => "Bien",
                >= 2.0m => "Passable",
                _ => "Insuffisant"
            };
        }

        private static string GetNiveauExpertiseLabel(int niveau)
        {
            return niveau switch
            {
                1 => "Débutant",
                2 => "Apprenti",
                3 => "Compétent",
                4 => "Expert",
                5 => "Maître",
                _ => "Non défini"
            };
        }

        private static decimal CalculerScoreRecommandation(SousTraitant sousTraitant)
        {
            decimal score = sousTraitant.NoteMoyenne * 2; // Note sur 10
            score += sousTraitant.NombreEvaluations * 0.1m; // Bonus pour expérience
            /*if (sousTraitant.AssuranceValide) score += 1;*/ // Bonus assurance
            return Math.Round(score, 2);
        }

        private async Task<object> GetTopSpecialites()
        {
            try
            {
                var specialites = await _sousTraitantService.GetAllSpecialitesAsync();
                // TODO: Implémenter le comptage des sous-traitants par spécialité
                return specialites.Take(5).Select(s => new { s.Nom, Nombre = 0 }); // Placeholder
            }
            catch
            {
                return new List<object>();
            }
        }

    }

}