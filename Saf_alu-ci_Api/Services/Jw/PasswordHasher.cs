using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace Saf_alu_ci_Api.Services.Jw
{
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            // Génère un sel de 128 bits (16 octets)
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Derive le hash avec PBKDF2
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            // Combine le sel + le hash pour le stocker
            return $"{Convert.ToBase64String(salt)}.{hashed}";
        }

        public static bool Verify(string password, string hashedPasswordWithSalt)
        {
            var parts = hashedPasswordWithSalt.Split('.');
            if (parts.Length != 2)
                return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            string expectedHash = parts[1];

            string actualHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            return actualHash == expectedHash;
        }
    }
}