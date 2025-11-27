using Microsoft.IdentityModel.Tokens;
using Saf_alu_ci_Api.Controllers.Utilisateurs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Saf_alu_ci_Api.Services
{
    public class AuthService
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;

        public AuthService(IConfiguration configuration)
        {
            _secretKey = configuration["Jwt:SecretKey"];
            _issuer = configuration["Jwt:Issuer"];
            _audience = configuration["Jwt:Audience"];
        }

        public string GenerateToken(Utilisateur user)
        {
            // ✅ CLAIMS REQUIS POUR LE DASHBOARD
            var claims = new List<Claim>
        {
            // ID de l'utilisateur - OBLIGATOIRE
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            
            // Rôle de l'utilisateur - OBLIGATOIRE
            new Claim("Role", user.Role?.Nom ?? "user"),
            
            // Claims supplémentaires (optionnels mais recommandés)
            new Claim(ClaimTypes.Name, user.Nom ?? ""),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim("Prenom", user.Prenom ?? ""),
        };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8), // Durée de validité
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
