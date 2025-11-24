using Microsoft.IdentityModel.Tokens;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Saf_alu_ci_Api.Services.Jw
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Génère un token JWT avec les Claims nécessaires pour le Dashboard
        /// </summary>
        /// <param name="user">L'utilisateur connecté</param>
        /// <returns>Token JWT sous forme de string</returns>
        public string GenerateToken(Utilisateur user)
        {
            // ✅ CLAIMS OBLIGATOIRES POUR LE DASHBOARD
            var claims = new List<Claim>
            {
                // ID de l'utilisateur - REQUIS par DashboardController
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                
                // Rôle de l'utilisateur - REQUIS par DashboardController
                new Claim("Role", user.Role?.Nom ?? "user"),
                
                // Claims supplémentaires (optionnels mais recommandés)
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, $"{user.Prenom} {user.Nom}"),
                new Claim("Prenom", user.Prenom ?? ""),
                new Claim("Nom", user.Nom ?? ""),
                new Claim("Username", user.Username ?? ""),
            };

            // Ajouter RoleId comme claim (optionnel)
            claims.Add(new Claim("RoleId", user.RoleId.ToString()));

            // Récupérer la clé secrète depuis appsettings.json
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey is not configured"))
            );

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Durée de validité du token (8 heures par défaut)
            var expirationHours = int.Parse(_configuration["Jwt:ExpirationHours"] ?? "8");

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expirationHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Génère un token JWT (version simplifiée - DÉPRÉCIÉ)
        /// Utilisez plutôt GenerateToken(Utilisateur user)
        /// </summary>
        //[Obsolete("Utilisez GenerateToken(Utilisateur user) à la place")]
        //public string GenerateToken(string email)
        //{
        //    // Cette méthode est conservée pour compatibilité mais devrait être évitée
        //    var claims = new List<Claim>
        //    {
        //        new Claim(ClaimTypes.Email, email),
        //    };

        //    var key = new SymmetricSecurityKey(
        //        Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"])
        //    );

        //    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        //    var expirationHours = int.Parse(_configuration["Jwt:ExpirationHours"] ?? "8");

        //    var token = new JwtSecurityToken(
        //        issuer: _configuration["Jwt:Issuer"],
        //        audience: _configuration["Jwt:Audience"],
        //        claims: claims,
        //        expires: DateTime.UtcNow.AddHours(expirationHours),
        //        signingCredentials: credentials
        //    );

        //    return new JwtSecurityTokenHandler().WriteToken(token);
        //}

        /// <summary>
        /// Valide un token JWT et retourne les claims
        /// </summary>
        //public ClaimsPrincipal? ValidateToken(string token)
        //{
        //    try
        //    {
        //        var tokenHandler = new JwtSecurityTokenHandler();
        //        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]);

        //        var validationParameters = new TokenValidationParameters
        //        {
        //            ValidateIssuerSigningKey = true,
        //            IssuerSigningKey = new SymmetricSecurityKey(key),
        //            ValidateIssuer = true,
        //            ValidIssuer = _configuration["Jwt:Issuer"],
        //            ValidateAudience = true,
        //            ValidAudience = _configuration["Jwt:Audience"],
        //            ValidateLifetime = true,
        //            ClockSkew = TimeSpan.Zero
        //        };

        //        var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
        //        return principal;
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}

        /// <summary>
        /// Extrait l'ID utilisateur depuis un token
        /// </summary>
        //public int? GetUserIdFromToken(string token)
        //{
        //    var principal = ValidateToken(token);
        //    var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        //    if (int.TryParse(userIdClaim, out int userId))
        //    {
        //        return userId;
        //    }

        //    return null;
        //}

        ///// <summary>
        ///// Extrait le rôle depuis un token
        ///// </summary>
        //public string? GetRoleFromToken(string token)
        //{
        //    var principal = ValidateToken(token);
        //    return principal?.FindFirst("Role")?.Value;
        //}
    }
}