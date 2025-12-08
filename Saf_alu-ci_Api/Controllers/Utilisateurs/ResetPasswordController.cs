using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    [ApiController]
    [Route("api/auth")]
    public class ResetPasswordController : ControllerBase
    {
        private readonly ResetPasswordService _resetPasswordService;

        public ResetPasswordController(ResetPasswordService resetPasswordService)
        {
            _resetPasswordService = resetPasswordService;
        }

        /// <summary>
        /// Demande de réinitialisation de mot de passe (envoi email)
        /// </summary>
        /// <remarks>
        /// POST /api/auth/forgot-password
        /// {
        ///   "email": "utilisateur@safalu.ci"
        /// }
        /// </remarks>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
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

            var result = await _resetPasswordService.DemanderReinitialisationAsync(request.Email);
            return Ok(result);
        }

        /// <summary>
        /// Valide un token de réinitialisation
        /// </summary>
        /// <remarks>
        /// GET /api/auth/validate-reset-token?token=xxx
        /// </remarks>
        [HttpGet("validate-reset-token")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateResetToken([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new
                {
                    success = false,
                    tokenValide = false,
                    message = "Token manquant"
                });
            }

            var result = await _resetPasswordService.ValiderTokenReinitialisationAsync(token);
            return Ok(result);
        }

        /// <summary>
        /// Réinitialise le mot de passe avec le token
        /// </summary>
        /// <remarks>
        /// POST /api/auth/reset-password
        /// {
        ///   "token": "xxx",
        ///   "nouveauMotDePasse": "NouveauPassword123!",
        ///   "confirmationMotDePasse": "NouveauPassword123!"
        /// }
        /// </remarks>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            // Validation
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

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token manquant"
                });
            }

            if (request.NouveauMotDePasse != request.ConfirmationMotDePasse)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Les mots de passe ne correspondent pas"
                });
            }

            var result = await _resetPasswordService.ReinitialiserdMotDePasseAsync(
                request.Token,
                request.NouveauMotDePasse
            );

            return Ok(result);
        }
    }
}
