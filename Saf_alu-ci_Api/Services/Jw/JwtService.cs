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


    }
}