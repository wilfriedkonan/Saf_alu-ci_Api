using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers.WhatsApp
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WhatsAppController : BaseController
    {
        private readonly WhatsAppService _whatsAppService;

        public WhatsAppController(WhatsAppService whatsAppService)
        {
            _whatsAppService = whatsAppService;
        }

        // =============================================
        // COMPTES WHATSAPP
        // =============================================

        /// <summary>
        /// GET /api/whatsapp/comptes
        /// Retourne tous les comptes WhatsApp actifs.
        /// Filtrable par service : ?service=Commercial
        /// </summary>
        [HttpGet("comptes")]
        public async Task<IActionResult> GetComptes([FromQuery] string? service = null)
        {
            try
            {
                var comptes = await _whatsAppService.GetAllComptesAsync(service);

                var result = comptes.Select(c => new
                {
                    c.Id,
                    c.NomInstance,
                    c.NomAffichage,
                    c.NumeroTelephone,
                    c.Description,
                    c.Service,
                    c.Actif,
                    c.Connecte,
                    c.DateConnexion,
                    c.DateCreation,
                    StatutConnexion = c.Connecte ? "Connecté" : "Déconnecté",
                    CouleurStatut = c.Connecte ? "#10b981" : "#6b7280"
                });

                return Ok(new
                {
                    comptes = result,
                    resume = new
                    {
                        NombreComptes = comptes.Count,
                        NombreConnectes = comptes.Count(c => c.Connecte),
                        Services = comptes
                            .Where(c => c.Service != null)
                            .GroupBy(c => c.Service)
                            .Select(g => new { Service = g.Key, Nombre = g.Count() })
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/whatsapp/comptes/{id}
        /// Retourne un compte WhatsApp par son Id.
        /// </summary>
        [HttpGet("comptes/{id}")]
        public async Task<IActionResult> GetCompte(int id)
        {
            try
            {
                var compte = await _whatsAppService.GetCompteByIdAsync(id);
                if (compte == null)
                    return NotFound(new { message = "Compte WhatsApp non trouvé" });

                return Ok(new
                {
                    compte.Id,
                    compte.NomInstance,
                    compte.NomAffichage,
                    compte.NumeroTelephone,
                    compte.Description,
                    compte.Service,
                    compte.Actif,
                    compte.Connecte,
                    compte.DateConnexion,
                    compte.DateCreation,
                    compte.DateModification,
                    StatutConnexion = compte.Connecte ? "Connecté" : "Déconnecté"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/whatsapp/comptes
        /// Crée un nouveau compte WhatsApp.
        /// </summary>
        [HttpPost("comptes")]
        public async Task<IActionResult> CreateCompte([FromBody] CreateWhatsAppCompteRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var utilisateurId = GetCurrentUserId();
                var compte = new WhatsAppCompte
                {
                    NomInstance = model.NomInstance,
                    NomAffichage = model.NomAffichage,
                    NumeroTelephone = model.NumeroTelephone,
                    Description = model.Description,
                    Service = model.Service,
                    Actif = true,
                    Connecte = false,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    UtilisateurCreation = utilisateurId,
                    UtilisateurModification = utilisateurId,
                };

                var id = await _whatsAppService.CreateCompteAsync(compte);

                return CreatedAtAction(nameof(GetCompte), new { id }, new
                {
                    message = "Compte WhatsApp créé avec succès",
                    id,
                    nomInstance = compte.NomInstance,
                    nomAffichage = compte.NomAffichage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/whatsapp/comptes/{id}
        /// Met à jour un compte WhatsApp existant.
        /// NomInstance n'est pas modifiable (clé fonctionnelle).
        /// </summary>
        [HttpPut("comptes/{id}")]
        public async Task<IActionResult> UpdateCompte(int id, [FromBody] UpdateWhatsAppCompteRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existing = await _whatsAppService.GetCompteByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Compte WhatsApp non trouvé" });

                var utilisateurId = GetCurrentUserId();
                var updated = await _whatsAppService.UpdateCompteAsync(id, model, utilisateurId);

                if (!updated)
                    return NotFound(new { message = "Aucune modification effectuée" });

                return Ok(new
                {
                    message = "Compte WhatsApp modifié avec succès",
                    id,
                    nomAffichage = model.NomAffichage
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// PATCH /api/whatsapp/comptes/{id}/connexion
        /// Met à jour le statut de connexion d'un compte (Connecte / Déconnecté).
        /// </summary>
        [HttpPatch("comptes/{id}/connexion")]
        public async Task<IActionResult> SetConnexion(int id, [FromBody] ConnexionWhatsAppRequest model)
        {
            try
            {
                var existing = await _whatsAppService.GetCompteByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Compte WhatsApp non trouvé" });

                var utilisateurId = GetCurrentUserId();
                await _whatsAppService.SetConnexionAsync(id, model.Connecte, utilisateurId);

                return Ok(new
                {
                    message = model.Connecte ? "Compte marqué comme connecté" : "Compte marqué comme déconnecté",
                    id,
                    connecte = model.Connecte,
                    dateConnexion = model.Connecte ? DateTime.UtcNow : (DateTime?)null,
                    statutAffichage = model.Connecte ? "Connecté" : "Déconnecté"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE /api/whatsapp/comptes/{id}
        /// Désactive (soft delete) un compte WhatsApp.
        /// </summary>
        [HttpDelete("comptes/{id}")]
        public async Task<IActionResult> DeleteCompte(int id)
        {
            try
            {
                var existing = await _whatsAppService.GetCompteByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Compte WhatsApp non trouvé" });

                var utilisateurId = GetCurrentUserId();
                await _whatsAppService.DeleteCompteAsync(id, utilisateurId);

                return Ok(new { message = "Compte WhatsApp supprimé avec succès", id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // TYPES DE MESSAGES  (lecture seule)
        // =============================================

        /// <summary>
        /// GET /api/whatsapp/messages-types
        /// Retourne tous les types de messages actifs.
        /// </summary>
        [HttpGet("messages-types")]
        public async Task<IActionResult> GetMessagesTypes()
        {
            try
            {
                var types = await _whatsAppService.GetAllTypesAsync();

                return Ok(new
                {
                    types,
                    nombreTypes = types.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/whatsapp/messages-types/{code}
        /// Retourne un type de message par son code.
        /// Ex : /api/whatsapp/messages-types/ENVOI_FACTURE
        /// </summary>
        [HttpGet("messages-types/{code}")]
        public async Task<IActionResult> GetMessageType(string code)
        {
            try
            {
                var type = await _whatsAppService.GetTypeByCodeAsync(code);
                if (type == null)
                    return NotFound(new { message = $"Type de message '{code}' introuvable" });

                return Ok(type);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        // =============================================
        // MESSAGES PRÉDÉFINIS
        // =============================================

        /// <summary>
        /// GET /api/whatsapp/messages-predefinis
        /// Retourne tous les messages prédéfinis actifs.
        /// Filtrable par type : ?typeCode=ENVOI_FACTURE
        /// </summary>
        [HttpGet("messages-predefinis")]
        public async Task<IActionResult> GetMessagesPredefinis([FromQuery] string? typeCode = null)
        {
            try
            {
                var messages = await _whatsAppService.GetAllMessagesAsync(typeCode);

                var result = messages.Select(m => new
                {
                    m.Id,
                    m.IdType,
                    m.Titre,
                    m.Contenu,
                    m.Actif,
                    m.DateCreation,
                    m.DateModification,
                    Type = new { m.Type!.Code, m.Type.Libelle },
                    // Parser la liste des variables en tableau
                    VariablesListe = ParseVariables(m.Variables)
                });

                return Ok(new
                {
                    messages = result,
                    nombreMessages = messages.Count,
                    filtreTypeCode = typeCode
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/whatsapp/messages-predefinis/{id}
        /// Retourne un message prédéfini par son Id.
        /// </summary>
        [HttpGet("messages-predefinis/{id}")]
        public async Task<IActionResult> GetMessagePredefini(int id)
        {
            try
            {
                var message = await _whatsAppService.GetMessageByIdAsync(id);
                if (message == null)
                    return NotFound(new { message = "Message prédéfini non trouvé" });

                return Ok(new
                {
                    message.Id,
                    message.IdType,
                    message.Titre,
                    message.Contenu,
                    message.Variables,
                    message.Actif,
                    message.DateCreation,
                    message.DateModification,
                    Type = new { message.Type!.Code, message.Type.Libelle, message.Type.Description },
                    VariablesListe = ParseVariables(message.Variables)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// GET /api/whatsapp/messages-predefinis/type/{code}
        /// Retourne tous les messages prédéfinis d'un type donné par son code.
        /// Ex : /api/whatsapp/messages-predefinis/type/RELANCE_PAIEMENT
        /// </summary>
        [HttpGet("messages-predefinis/type/{code}")]
        public async Task<IActionResult> GetMessagesPredefinisByType(string code)
        {
            try
            {
                var type = await _whatsAppService.GetTypeByCodeAsync(code);
                if (type == null)
                    return NotFound(new { message = $"Type de message '{code}' introuvable" });

                var messages = await _whatsAppService.GetMessagesByTypeCodeAsync(code);

                return Ok(new
                {
                    type = new { type.Code, type.Libelle, type.Description },
                    messages = messages.Select(m => new
                    {
                        m.Id,
                        m.Titre,
                        m.Contenu,
                        m.Variables,
                        m.Actif,
                        VariablesListe = ParseVariables(m.Variables)
                    }),
                    nombreMessages = messages.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/whatsapp/messages-predefinis
        /// Crée un nouveau message prédéfini.
        /// </summary>
        [HttpPost("messages-predefinis")]
        public async Task<IActionResult> CreateMessagePredefini(
            [FromBody] CreateWhatsAppMessagePredefiniRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var utilisateurId = GetCurrentUserId();
                var message = new WhatsAppMessagePredefini
                {
                    IdType = model.IdType,
                    Titre = model.Titre,
                    Contenu = model.Contenu,
                    Variables = model.Variables,
                    Actif = true,
                    DateCreation = DateTime.UtcNow,
                    DateModification = DateTime.UtcNow,
                    UtilisateurCreation = utilisateurId,
                    UtilisateurModification = utilisateurId,
                };

                var id = await _whatsAppService.CreateMessageAsync(message);

                return CreatedAtAction(nameof(GetMessagePredefini), new { id }, new
                {
                    message = "Message prédéfini créé avec succès",
                    id,
                    titre = model.Titre
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// PUT /api/whatsapp/messages-predefinis/{id}
        /// Met à jour un message prédéfini existant.
        /// IdType n'est pas modifiable.
        /// </summary>
        [HttpPut("messages-predefinis/{id}")]
        public async Task<IActionResult> UpdateMessagePredefini(
            int id, [FromBody] UpdateWhatsAppMessagePredefiniRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var existing = await _whatsAppService.GetMessageByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Message prédéfini non trouvé" });

                var utilisateurId = GetCurrentUserId();
                var updated = await _whatsAppService.UpdateMessageAsync(id, model, utilisateurId);

                if (!updated)
                    return NotFound(new { message = "Aucune modification effectuée" });

                return Ok(new
                {
                    message = "Message prédéfini modifié avec succès",
                    id,
                    titre = model.Titre,
                    actif = model.Actif
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE /api/whatsapp/messages-predefinis/{id}
        /// Désactive (soft delete) un message prédéfini.
        /// </summary>
        [HttpDelete("messages-predefinis/{id}")]
        public async Task<IActionResult> DeleteMessagePredefini(int id)
        {
            try
            {
                var existing = await _whatsAppService.GetMessageByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = "Message prédéfini non trouvé" });

                var utilisateurId = GetCurrentUserId();
                await _whatsAppService.DeleteMessageAsync(id, utilisateurId);

                return Ok(new { message = "Message prédéfini supprimé avec succès", id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur serveur", error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/whatsapp/messages-predefinis/{id}/previsualiser
        /// Substitue les {VARIABLES} du message par les valeurs fournies
        /// et retourne le contenu résolu + les variables manquantes.
        /// </summary>
        [HttpPost("messages-predefinis/{id}/previsualiser")]
        public async Task<IActionResult> PrevisualiserMessage(
            int id, [FromBody] PrevisualiserMessageRequest model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var (contenuResolu, variablesManquantes) =
                    await _whatsAppService.PrevisualiserMessageAsync(id, model.Variables);

                return Ok(new
                {
                    messageId = id,
                    contenuResolu,
                    estComplet = !variablesManquantes.Any(),
                    variablesManquantes,
                    nombreManquantes = variablesManquantes.Count
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
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
            return int.TryParse(userIdClaim, out var userId) ? userId : 3;
        }

        /// <summary>
        /// Transforme la chaîne "{PRENOM},{NOM},{EMAIL}" en tableau ["PRENOM", "NOM", "EMAIL"].
        /// </summary>
        private static List<string> ParseVariables(string? variables)
        {
            if (string.IsNullOrWhiteSpace(variables))
                return new List<string>();

            return variables
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim().Trim('{', '}'))
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }
}