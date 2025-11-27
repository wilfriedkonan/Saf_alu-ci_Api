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
    }
}