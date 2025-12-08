using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    [ApiController]
    [Route("api/utilisateurs")]
    public class UtilisateurInvitationController : BaseController
    {
        private readonly UtilisateurInvitationService _invitationService;

        public UtilisateurInvitationController(UtilisateurInvitationService invitationService)
        {
            _invitationService = invitationService;
        }

        /// <summary>
        /// Invite un nouvel utilisateur (création compte inactif + envoi email)
        /// </summary>
        /// <remarks>
        /// POST /api/utilisateurs/inviter
        /// {
        ///   "email": "nouveau@safalu.ci",
        ///   "prenom": "Jean",
        ///   "nom": "Dupont",
        ///   "telephone": "+225 07 12 34 56 78",
        ///   "roleId": 3
        /// }
        /// </remarks>
        [HttpPost("inviter")]
        [Authorize] // Seuls les admins peuvent inviter
        public async Task<IActionResult> InviterUtilisateur([FromBody] InviterUtilisateurRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Données invalides",
                    errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList()
                });
            }

            var result = await _invitationService.InviterUtilisateurAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Valide un token d'invitation et retourne les infos utilisateur
        /// </summary>
        /// <remarks>
        /// GET /api/utilisateurs/invitation/valider?token=xxx
        /// </remarks>
        [HttpGet("invitation/valider")]
        [AllowAnonymous]
        public async Task<IActionResult> ValiderToken([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token manquant"
                });
            }

            var result = await _invitationService.ValiderTokenAsync(token);
            return Ok(result);
        }

        /// <summary>
        /// Complète l'inscription (définir mot de passe + activer compte)
        /// </summary>
        /// <remarks>
        /// POST /api/utilisateurs/completer-inscription
        /// {
        ///   "token": "xxx",
        ///   "username": "jean.dupont",
        ///   "motDePasse": "MonMotDePasse123!",
        ///   "confirmationMotDePasse": "MonMotDePasse123!"
        /// }
        /// </remarks>
        [HttpPost("completer-inscription")]
        [AllowAnonymous]
        public async Task<IActionResult> CompleterInscription([FromBody] CompleterInscriptionRequest request)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token manquant"
                });
            }

            if (string.IsNullOrWhiteSpace(request.MotDePasse) || request.MotDePasse.Length < 8)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Le mot de passe doit contenir au moins 8 caractères"
                });
            }

            if (request.MotDePasse != request.ConfirmationMotDePasse)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Les mots de passe ne correspondent pas"
                });
            }

            var result = await _invitationService.CompleterInscriptionAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Renvoie un email d'invitation à un utilisateur inactif
        /// </summary>
        /// <remarks>
        /// POST /api/utilisateurs/{id}/renvoyer-invitation
        /// </remarks>
        [HttpPost("{id}/renvoyer-invitation")]
        [Authorize]
        public async Task<IActionResult> RenvoyerInvitation(int id)
        {
            var result = await _invitationService.RenvoyerInvitationAsync(id);
            return Ok(result);
        }
    }
}
