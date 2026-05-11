using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.DevisFournisseur
{
    // ============================================================
    // FOURNISSEURS — CRUD
    // ============================================================

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FournisseursController : BaseController
    {
        private readonly DevisFournisseurService _service;
        public FournisseursController(DevisFournisseurService service) => _service = service;

        /// <summary>GET /api/fournisseurs?search=</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search = null)
        {
            try
            {
                var list = await _service.GetFournisseursAsync(search);
                return Ok(new { fournisseurs = list, total = list.Count });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>GET /api/fournisseurs/{id}</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var f = await _service.GetFournisseurByIdAsync(id);
                if (f == null) return NotFound(new { message = "Fournisseur introuvable" });
                return Ok(f);
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>POST /api/fournisseurs</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFournisseurRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var userId = GetCurrentUserId();
                var f = new Fournisseur
                {
                    Nom = req.Nom,
                    RaisonSociale = req.RaisonSociale,
                    Email = req.Email,
                    Telephone = req.Telephone,
                    Adresse = req.Adresse,
                    Ville = req.Ville,
                    NomContact = req.NomContact,
                    TelephoneContact = req.TelephoneContact,
                    EmailContact = req.EmailContact,
                    Ncc = req.Ncc,
                    UtilisateurCreation = userId,
                };
                var id = await _service.CreateFournisseurAsync(f);
                return CreatedAtAction(nameof(GetById), new { id },
                    new { message = "Fournisseur créé avec succès", id, nom = req.Nom });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>PUT /api/fournisseurs/{id}</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateFournisseurRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var updated = await _service.UpdateFournisseurAsync(id, req, GetCurrentUserId());
                if (!updated) return NotFound(new { message = "Fournisseur introuvable" });
                return Ok(new { message = "Fournisseur mis à jour avec succès", id });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>DELETE /api/fournisseurs/{id}</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _service.DeleteFournisseurAsync(id);
                if (!deleted) return NotFound(new { message = "Fournisseur introuvable" });
                return Ok(new { message = "Fournisseur supprimé avec succès", id });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 1;
        }
    }

    // ============================================================
    // DEVIS FOURNISSEUR — PRINCIPAL
    // ============================================================

    [ApiController]
    [Route("api/devis-fournisseur")]
    [Authorize]
    public class DevisFournisseurController : BaseController
    {
        private readonly DevisFournisseurService _service;
        private readonly IConfiguration _config;

        public DevisFournisseurController(DevisFournisseurService service, IConfiguration config)
        {
            _service = service;
            _config = config;
        }

        // ── DEVIS ────────────────────────────────────────────────

        /// <summary>
        /// GET /api/devis-fournisseur?statut=EnCours&typeDevis=Technique
        /// Liste tous les devis avec filtres optionnels.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? statut = null,
            [FromQuery] string? typeDevis = null)
        {
            try
            {
                var list = await _service.GetDevisListAsync(statut, typeDevis);
                return Ok(new
                {
                    devis = list,
                    resume = new
                    {
                        Total = list.Count,
                        Brouillons = list.Count(d => d.Statut == "Brouillon"),
                        EnCours = list.Count(d => d.Statut == "EnCours"),
                        Clotures = list.Count(d => d.Statut == "Cloture"),
                        Selectionnes = list.Count(d => d.Statut == "Selectionne"),
                    }
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// GET /api/devis-fournisseur/{id}
        /// Retourne le détail complet d'un devis (sections, lignes, demandes).
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var devis = await _service.GetDevisDetailAsync(id);
                if (devis == null) return NotFound(new { message = "Devis introuvable" });
                return Ok(devis);
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// POST /api/devis-fournisseur
        /// Crée un devis (Classique ou Technique) avec ses lignes/sections.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDevisFournisseurRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                if (req.TypeDevis is not ("Classique" or "Technique"))
                    return BadRequest(new { message = "TypeDevis doit être 'Classique' ou 'Technique'" });

                var id = await _service.CreateDevisAsync(req, GetCurrentUserId());
                return CreatedAtAction(nameof(GetById), new { id },
                    new { message = "Devis créé avec succès", id, typeDevis = req.TypeDevis });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// PUT /api/devis-fournisseur/{id}
        /// Modifie l'en-tête d'un devis (titre, délai, remises globales).
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDevisFournisseurRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var updated = await _service.UpdateDevisAsync(id, req, GetCurrentUserId());
                if (!updated) return NotFound(new { message = "Devis introuvable" });
                return Ok(new { message = "Devis modifié avec succès", id });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// PATCH /api/devis-fournisseur/{id}/cloturer
        /// Clôture manuellement un devis (Brouillon ou EnCours → Cloture).
        /// </summary>
        [HttpPatch("{id}/cloturer")]
        public async Task<IActionResult> Cloturer(int id)
        {
            try
            {
                var ok = await _service.CloturerDevisAsync(id, GetCurrentUserId());
                if (!ok) return BadRequest(new { message = "Devis introuvable ou déjà clôturé/sélectionné" });
                return Ok(new { message = "Devis clôturé avec succès", id });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        // ── SECTIONS (Technique) ─────────────────────────────────

        /// <summary>POST /api/devis-fournisseur/{id}/sections</summary>
        [HttpPost("{id}/sections")]
        public async Task<IActionResult> CreateSection(int id, [FromBody] CreateSectionRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var sectionId = await _service.CreateSectionAsync(id, req);
                return Ok(new { message = "Section créée avec succès", id = sectionId });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>PUT /api/devis-fournisseur/{id}/sections/{sectionId}</summary>
        [HttpPut("{id}/sections/{sectionId}")]
        public async Task<IActionResult> UpdateSection(int id, int sectionId, [FromBody] UpdateSectionRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var ok = await _service.UpdateSectionAsync(sectionId, req);
                if (!ok) return NotFound(new { message = "Section introuvable" });
                return Ok(new { message = "Section mise à jour", id = sectionId });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>DELETE /api/devis-fournisseur/{id}/sections/{sectionId}</summary>
        [HttpDelete("{id}/sections/{sectionId}")]
        public async Task<IActionResult> DeleteSection(int id, int sectionId)
        {
            try
            {
                var ok = await _service.DeleteSectionAsync(sectionId);
                if (!ok) return NotFound(new { message = "Section introuvable" });
                return Ok(new { message = "Section supprimée avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        // ── LIGNES ───────────────────────────────────────────────

        /// <summary>POST /api/devis-fournisseur/{id}/lignes</summary>
        [HttpPost("{id}/lignes")]
        public async Task<IActionResult> CreateLigne(int id, [FromBody] CreateLigneRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var ligneId = await _service.CreateLigneAsync(id, req);
                return Ok(new { message = "Ligne créée avec succès", id = ligneId });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>PUT /api/devis-fournisseur/{id}/lignes/{ligneId}</summary>
        [HttpPut("{id}/lignes/{ligneId}")]
        public async Task<IActionResult> UpdateLigne(int id, int ligneId, [FromBody] UpdateLigneRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var ok = await _service.UpdateLigneAsync(ligneId, req);
                if (!ok) return NotFound(new { message = "Ligne introuvable" });
                return Ok(new { message = "Ligne mise à jour", id = ligneId });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>DELETE /api/devis-fournisseur/{id}/lignes/{ligneId}</summary>
        [HttpDelete("{id}/lignes/{ligneId}")]
        public async Task<IActionResult> DeleteLigne(int id, int ligneId)
        {
            try
            {
                var ok = await _service.DeleteLigneAsync(ligneId);
                if (!ok) return NotFound(new { message = "Ligne introuvable" });
                return Ok(new { message = "Ligne supprimée avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        // ── DEMANDES (envoi WhatsApp) ─────────────────────────────

        /// <summary>
        /// POST /api/devis-fournisseur/{id}/demandes
        /// Génère les tokens + OTP et prépare les messages WhatsApp pour chaque fournisseur.
        /// Retourne les messages prêts à envoyer (OTP inclus, visible une seule fois).
        /// </summary>
        [HttpPost("{id}/demandes")]
        public async Task<IActionResult> EnvoyerDemandes(int id, [FromBody] EnvoyerDemandesRequest req)
        {
            try
            {
                if (!req.FournisseurIds.Any())
                    return BadRequest(new { message = "Sélectionnez au moins un fournisseur" });

                var baseUrl = _config["App:FrontendUrl"] ?? "https://app.saf-alu.ci";

                // Template de message par défaut (peut venir de WhatsAppMessagesPredéfinis)
                var template = req.MessagePersonnalise ?? @"📋 Bonjour *{NOM_CONTACT}*,

*{NOM_ENTREPRISE}* vous sollicite pour un devis.

📌 *Référence :* {REFERENCE_DEMANDE}
📅 *Date :* {DATE_DEMANDE}
⏰ *Délai de réponse :* {DATE_LIMITE}

{DESCRIPTION_DEMANDE}

📝 *Renseigner votre devis :*
{LIEN_DEVIS}";

                var demandes = await _service.EnvoyerDemandesAsync(id, req, GetCurrentUserId(), baseUrl, template);

                // Retourner les messages complets avec OTP pour l'envoi WhatsApp
                var result = demandes.Select(d => new
                {
                    d.Id,
                    d.FournisseurId,
                    d.FournisseurNom,
                    d.FournisseurTelephone,
                    d.Token,
                    Otp = d.Otp,           // OTP visible UNE SEULE FOIS ici
                    d.DateExpiration,
                    d.MessageWhatsApp,
                    LienDevis = $"{baseUrl}/devis-fournisseur/{d.Token}",
                });

                return Ok(new
                {
                    message = $"{demandes.Count} demande(s) créée(s) avec succès",
                    demandes = result,
                    avertissement = "Les OTP sont visibles ici une seule fois. Envoyez les messages WhatsApp immédiatement."
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// DELETE /api/devis-fournisseur/{id}/demandes/{demandeId}
        /// Annule une demande (seulement si statut EnAttente).
        /// </summary>
        [HttpDelete("{id}/demandes/{demandeId}")]
        public async Task<IActionResult> AnnulerDemande(int id, int demandeId)
        {
            try
            {
                var ok = await _service.AnnulerDemandeAsync(demandeId);
                if (!ok) return BadRequest(new { message = "Demande introuvable ou déjà ouverte par le fournisseur" });
                return Ok(new { message = "Demande annulée avec succès" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        // ── COMPARAISON ET SÉLECTION ─────────────────────────────

        /// <summary>
        /// GET /api/devis-fournisseur/{id}/comparaison
        /// Tableau de comparaison des offres reçues, avec rangs par prix.
        /// </summary>
        [HttpGet("{id}/comparaison")]
        public async Task<IActionResult> GetComparaison(int id)
        {
            try
            {
                var comparaison = await _service.GetComparaisonAsync(id);
                if (comparaison == null) return NotFound(new { message = "Devis introuvable" });
                return Ok(comparaison);
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// POST /api/devis-fournisseur/{id}/selectionner-fournisseur
        /// Sélectionne un fournisseur pour l'ensemble du devis.
        /// </summary>
        [HttpPost("{id}/selectionner-fournisseur")]
        public async Task<IActionResult> SelectionnerFournisseur(
            int id, [FromBody] SelectionnerFournisseurRequest req)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                await _service.SelectionnerFournisseurAsync(id, req, GetCurrentUserId());
                return Ok(new { message = "Fournisseur sélectionné avec succès", devisId = id, demandeId = req.DemandeId });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// POST /api/devis-fournisseur/{id}/selectionner-lignes
        /// Sélectionne le meilleur fournisseur ligne par ligne.
        /// Body : { "selectionsParLigne": { "ligneId": demandeId, ... } }
        /// </summary>
        [HttpPost("{id}/selectionner-lignes")]
        public async Task<IActionResult> SelectionnerLignes(
            int id, [FromBody] SelectionnerLignesRequest req)
        {
            try
            {
                if (!req.SelectionParLigne.Any())
                    return BadRequest(new { message = "Aucune sélection fournie" });

                await _service.SelectionnerLignesAsync(id, req);
                return Ok(new
                {
                    message = "Sélection par ligne enregistrée avec succès",
                    devisId = id,
                    nombreLignes = req.SelectionParLigne.Count
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 1;
        }
    }

    // ============================================================
    // ACCÈS PUBLIC — FOURNISSEUR (sans authentification)
    // ============================================================

    [ApiController]
    [Route("api/devis-fournisseur/public")]
    [AllowAnonymous]
    public class DevisFournisseurPublicController : ControllerBase
    {
        private readonly DevisFournisseurService _service;
        public DevisFournisseurPublicController(DevisFournisseurService service) => _service = service;

        /// <summary>
        /// GET /api/devis-fournisseur/public/{token}
        /// Retourne les informations du devis accessibles au fournisseur.
        /// Déclenche la mise à jour du statut en LienOuvert.
        /// </summary>
        [HttpGet("{token:guid}")]
        public async Task<IActionResult> GetDevisPublic(Guid token)
        {
            try
            {
                var (devis, erreur) = await _service.GetDevisPublicAsync(token);
                if (erreur != null) return BadRequest(new { message = erreur });
                return Ok(devis);
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// POST /api/devis-fournisseur/public/{token}/valider-otp
        /// Valide l'OTP reçu par WhatsApp. Bloque après 3 tentatives erronées.
        /// </summary>
        [HttpPost("{token:guid}/valider-otp")]
        public async Task<IActionResult> ValiderOtp(Guid token, [FromBody] ValiderOtpRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Otp))
                    return BadRequest(new { message = "Le code OTP est requis" });

                var (ok, message) = await _service.ValiderOtpAsync(token, req.Otp);
                if (!ok) return BadRequest(new { message, otpValide = false });
                return Ok(new { message, otpValide = true });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }

        /// <summary>
        /// POST /api/devis-fournisseur/public/{token}/soumettre
        /// Le fournisseur soumet ses prix pour chaque ligne du devis.
        /// Nécessite un OTP préalablement validé.
        /// </summary>
        [HttpPost("{token:guid}/soumettre")]
        public async Task<IActionResult> Soumettre(Guid token, [FromBody] SoumettreReponsesRequest req)
        {
            try
            {
                if (!req.Reponses.Any())
                    return BadRequest(new { message = "Aucun prix renseigné" });

                var (ok, message) = await _service.SoumettreReponsesAsync(token, req);
                if (!ok) return BadRequest(new { message });
                return Ok(new { message });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Erreur serveur", error = ex.Message }); }
        }
    }
}