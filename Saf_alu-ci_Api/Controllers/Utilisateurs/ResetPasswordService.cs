using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Services.Jw;
using Saf_alu_ci_Api.Services.messagerie;
using System.Security.Cryptography;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    public class ResetPasswordService
    {
        private readonly string _connectionString;
        private readonly SmtpEmailService _smtpEmailService;
        private readonly IConfiguration _configuration;

        public ResetPasswordService(IConfiguration configuration, SmtpEmailService smtpEmailService)
        {
            _configuration = configuration;
            _smtpEmailService = smtpEmailService;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        /// <summary>
        /// Initie une demande de réinitialisation de mot de passe
        /// </summary>
        public async Task<object> DemanderReinitialisationAsync(string email)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // 1. Vérifier si l'utilisateur existe
                var query = @"
                    SELECT Id, Email, Prenom, Nom, Actif 
                    FROM Utilisateurs 
                    WHERE Email = @Email";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Email", email);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    // Pour des raisons de sécurité, on retourne toujours succès
                    // même si l'email n'existe pas (évite l'énumération d'emails)
                    return new
                    {
                        success = true,
                        message = "Si cet email existe, un lien de réinitialisation a été envoyé.",
                        email = email
                    };
                }

                int userId = reader.GetInt32(0);
                string userEmail = reader.GetString(1);
                string prenom = reader.GetString(2);
                string nom = reader.GetString(3);
                bool actif = reader.GetBoolean(4);

                reader.Close();

                // 2. Vérifier si le compte est actif
                if (!actif)
                {
                    return new
                    {
                        success = false,
                        message = "Ce compte est désactivé. Contactez l'administrateur."
                    };
                }

                // 3. Générer un token sécurisé
                string token = GenerateSecureToken();
                DateTime tokenExpiration = DateTime.UtcNow.AddHours(1); // 1 heure de validité

                // 4. Enregistrer le token en base
                var updateQuery = @"
                    UPDATE Utilisateurs 
                    SET ResetPasswordToken = @Token,
                        ResetPasswordTokenExpiration = @Expiration,
                        DateModification = GETDATE()
                    WHERE Id = @Id";

                using var updateCmd = new SqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@Token", token);
                updateCmd.Parameters.AddWithValue("@Expiration", tokenExpiration);
                updateCmd.Parameters.AddWithValue("@Id", userId);

                await updateCmd.ExecuteNonQueryAsync();

                // 5. Générer l'URL de réinitialisation
                string baseUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:3000";
                string resetUrl = $"{baseUrl}/auth/reset-password?token={token}";

                // 6. Envoyer l'email
                bool emailEnvoye = await _smtpEmailService.SendResetPasswordEmailAsync(
                    userEmail,
                    $"{prenom} {nom}",
                    resetUrl,
                    tokenExpiration
                );

                if (!emailEnvoye)
                {
                    return new
                    {
                        success = false,
                        message = "Erreur lors de l'envoi de l'email. Veuillez réessayer."
                    };
                }

                return new
                {
                    success = true,
                    message = "Un email de réinitialisation a été envoyé à votre adresse.",
                    email = userEmail,
                    tokenExpiration = tokenExpiration
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur réinitialisation: {ex.Message}");
                return new
                {
                    success = false,
                    message = "Une erreur est survenue. Veuillez réessayer."
                };
            }
        }

        /// <summary>
        /// Valide un token de réinitialisation
        /// </summary>
        public async Task<object> ValiderTokenReinitialisationAsync(string token)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT Id, Email, Prenom, Nom, ResetPasswordTokenExpiration, Actif
                    FROM Utilisateurs 
                    WHERE ResetPasswordToken = @Token";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Token", token);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new
                    {
                        success = false,
                        tokenValide = false,
                        message = "Token invalide ou expiré."
                    };
                }

                string email = reader.GetString(1);
                string prenom = reader.GetString(2);
                string nom = reader.GetString(3);
                DateTime tokenExpiration = reader.GetDateTime(4);
                bool actif = reader.GetBoolean(5);

                // Vérifier si le token est expiré
                if (tokenExpiration < DateTime.UtcNow)
                {
                    return new
                    {
                        success = false,
                        tokenValide = false,
                        message = "Ce lien a expiré. Veuillez faire une nouvelle demande."
                    };
                }

                // Vérifier si le compte est actif
                if (!actif)
                {
                    return new
                    {
                        success = false,
                        tokenValide = false,
                        message = "Ce compte est désactivé."
                    };
                }

                return new
                {
                    success = true,
                    tokenValide = true,
                    email = email,
                    nomComplet = $"{prenom} {nom}",
                    tokenExpiration = tokenExpiration,
                    message = "Token valide"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur validation token: {ex.Message}");
                return new
                {
                    success = false,
                    tokenValide = false,
                    message = "Erreur lors de la validation."
                };
            }
        }

        /// <summary>
        /// Réinitialise le mot de passe avec le token
        /// </summary>
        public async Task<object> ReinitialiserdMotDePasseAsync(string token, string nouveauMotDePasse)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // 1. Valider le token
                var query = @"
                    SELECT Id, Email, Prenom, Nom, ResetPasswordTokenExpiration, Actif
                    FROM Utilisateurs 
                    WHERE ResetPasswordToken = @Token";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Token", token);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new
                    {
                        success = false,
                        message = "Token invalide ou expiré."
                    };
                }

                int userId = reader.GetInt32(0);
                string email = reader.GetString(1);
                DateTime tokenExpiration = reader.GetDateTime(4);
                bool actif = reader.GetBoolean(5);

                reader.Close();

                // Vérifier expiration
                if (tokenExpiration < DateTime.UtcNow)
                {
                    return new
                    {
                        success = false,
                        message = "Ce lien a expiré. Veuillez faire une nouvelle demande."
                    };
                }

                // Vérifier statut compte
                if (!actif)
                {
                    return new
                    {
                        success = false,
                        message = "Ce compte est désactivé."
                    };
                }

                // 2. Hash du nouveau mot de passe
                string passwordHash = PasswordHasher.Hash(nouveauMotDePasse);

                // 3. Mettre à jour le mot de passe et supprimer le token
                var updateQuery = @"
                    UPDATE Utilisateurs 
                    SET MotDePasseHash = @MotDePasseHash,
                        ResetPasswordToken = NULL,
                        ResetPasswordTokenExpiration = NULL,
                        DateModification = GETDATE()
                    WHERE Id = @Id";

                using var updateCmd = new SqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@MotDePasseHash", passwordHash);
                updateCmd.Parameters.AddWithValue("@Id", userId);

                await updateCmd.ExecuteNonQueryAsync();

                return new
                {
                    success = true,
                    message = "Votre mot de passe a été réinitialisé avec succès. Vous pouvez maintenant vous connecter.",
                    email = email
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur réinitialisation mot de passe: {ex.Message}");
                return new
                {
                    success = false,
                    message = "Erreur lors de la réinitialisation. Veuillez réessayer."
                };
            }
        }

        /// <summary>
        /// Génère un token sécurisé
        /// </summary>
        private string GenerateSecureToken()
        {
            byte[] tokenBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            return Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
