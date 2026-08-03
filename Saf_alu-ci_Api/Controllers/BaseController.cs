using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saf_alu_ci_Api.Controllers
{
    public abstract class BaseController : ControllerBase
    {
        protected int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? User.FindFirst("Id")?.Value;

            return claim != null ? int.Parse(claim) : null;
        }

        protected string? GetCurrentUserRole()
        {
            // Essaie les noms les plus courants dans l'ordre
            return User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value      // URI long Microsoft
                ?? User.FindFirst("role")?.Value                                        // nom court JWT standard
                ?? User.FindFirst("Role")?.Value                                        // variante casse
                ?? User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                ?? null;
        }
    }
}
