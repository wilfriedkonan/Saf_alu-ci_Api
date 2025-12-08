using brevo_csharp.Client;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.Data.SqlClient;
using Saf_alu_ci_Api.Services.Jw;
using Saf_alu_ci_Api.Services.messagerie;
using System.Security.Cryptography;

namespace Saf_alu_ci_Api.Controllers.Utilisateurs
{
    public class UtilisateurInvitationService
    {
        private readonly string _connectionString;
        private readonly SmtpEmailService _smtpEmailService;
        private readonly IConfiguration _configuration;

        public UtilisateurInvitationService(
            string connectionString,
            SmtpEmailService smtpEmailService,
            IConfiguration configuration)
        {
            _connectionString = connectionString;
            _smtpEmailService = smtpEmailService;
            _configuration = configuration;
        }

        /// <summary>
        /// Crée un nouvel utilisateur inactif et envoie un email d'invitation
        /// </summary>
        public async Task<object> InviterUtilisateurAsync(InviterUtilisateurRequest request)
        {
            try
            {
                // 1. Vérifier si l'email existe déjà
                if (await EmailExisteAsync(request.Email))
                {
                    return new
                    {
                        success = false,
                        message = "Cet email est déjà utilisé"
                    };
                }

                // 2. Générer un token unique
                string token = GenerateSecureToken();
                DateTime tokenExpiration = DateTime.UtcNow.AddHours(48); // 48h pour compléter l'inscription

                // 3. Créer l'utilisateur inactif
                int utilisateurId = await CreerUtilisateurInactifAsync(request, token, tokenExpiration);

                // 4. Générer l'URL de callback
                string baseUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:3000";
                string callbackUrl = $"{baseUrl}/rejoins-nous?token={token}";

                // 5. Envoyer l'email d'invitation
                bool emailEnvoye = await _smtpEmailService.SendInvitationEmailAsync(
                    request.Email,
                    $"{request.Prenom} {request.Nom}",
                    callbackUrl,
                    tokenExpiration
                );

                if (!emailEnvoye)
                {
                    // Rollback: supprimer l'utilisateur créé
                    await SupprimerUtilisateurAsync(utilisateurId);

                    return new
                    {
                        success = false,
                        message = "Erreur lors de l'envoi de l'email d'invitation"
                    };
                }

                return new
                {
                    success = true,
                    message = "Invitation envoyée avec succès",
                    data = new
                    {
                        Id = utilisateurId,
                        email = request.Email,
                        nomComplet = $"{request.Prenom} {request.Nom}",
                        emailEnvoye = true,
                        tokenExpiration = tokenExpiration
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = $"Erreur lors de l'invitation: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Valide un token et retourne les informations utilisateur
        /// </summary>
        public async Task<object> ValiderTokenAsync(string token)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var query = @"
                    SELECT Email, Prenom, Nom, TokenExpiration 
                    FROM Utilisateurs 
                    WHERE InvitationToken = @Token 
                    AND Actif = 0";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Token", token);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new
                    {
                        success = false,
                        message = "Token invalide",
                        data = new { tokenValide = false }
                    };
                }

                string email = reader.GetString(0);
                string prenom = reader.GetString(1);
                string nom = reader.GetString(2);
                DateTime tokenExpiration = reader.GetDateTime(3);

                bool tokenValide = tokenExpiration > DateTime.UtcNow;

                return new
                {
                    success = tokenValide,
                    message = tokenValide ? "Token valide" : "Token expiré",
                    data = new
                    {
                        email = email,
                        prenom = prenom,
                        nom = nom,
                        tokenExpiration = tokenExpiration,
                        tokenValide = tokenValide
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = $"Erreur: {ex.Message}",
                    data = new { tokenValide = false }
                };
            }
        }

        /// <summary>
        /// Complète l'inscription avec mot de passe et active le compte
        /// </summary>
        public async Task<object> CompleterInscriptionAsync(CompleterInscriptionRequest request)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // 1. Vérifier le token
                var query = @"
                    SELECT Id, Email, Prenom, Nom, TokenExpiration 
                    FROM Utilisateurs 
                    WHERE InvitationToken = @Token 
                    AND Actif = 0 
                    AND TokenExpiration > GETUTCDATE()";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Token", request.Token);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new
                    {
                        success = false,
                        message = "Token invalide ou expiré"
                    };
                }

                int utilisateurId = reader.GetInt32(0);
                string email = reader.GetString(1);
                string prenom = reader.GetString(2);
                string nom = reader.GetString(3);

                reader.Close();

                // 2. Hash du mot de passe
                string passwordHash = PasswordHasher.Hash(request.MotDePasse);

                // 3. Mettre à jour l'utilisateur
                var updateQuery = @"
                    UPDATE Utilisateurs 
                    SET MotDePasseHash = @MotDePasse,
                        Username = @Username,
                        Actif = 1,
                        InvitationToken = NULL,
                        TokenExpiration = NULL,
                        DateModification = GETDATE()
                    WHERE Id = @Id";

                using var updateCmd = new SqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@MotDePasse", passwordHash);
                updateCmd.Parameters.AddWithValue("@Username", request.Username ?? email.Split('@')[0]);
                updateCmd.Parameters.AddWithValue("@Id", utilisateurId);

                await updateCmd.ExecuteNonQueryAsync();

                return new
                {
                    success = true,
                    message = "Inscription complétée avec succès. Vous pouvez maintenant vous connecter.",
                    data = new
                    {
                        utilisateurId = utilisateurId,
                        email = email,
                        nomComplet = $"{prenom} {nom}"
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = $"Erreur lors de la complétion de l'inscription: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Renvoie un email d'invitation
        /// </summary>
        public async Task<object> RenvoyerInvitationAsync(int utilisateurId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // 1. Récupérer les infos utilisateur
                var query = @"
                    SELECT Email, Prenom, Nom, Actif 
                    FROM Utilisateurs 
                    WHERE Id = @Id";

                using var cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Id", utilisateurId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new
                    {
                        success = false,
                        message = "Utilisateur non trouvé"
                    };
                }

                string email = reader.GetString(0);
                string prenom = reader.GetString(1);
                string nom = reader.GetString(2);
                bool actif = reader.GetBoolean(3);

                reader.Close();

                if (actif)
                {
                    return new
                    {
                        success = false,
                        message = "Cet utilisateur est déjà actif"
                    };
                }

                // 2. Générer nouveau token
                string newToken = GenerateSecureToken();
                DateTime newExpiration = DateTime.UtcNow.AddHours(48);

                // 3. Mettre à jour le token
                var updateQuery = @"
                    UPDATE Utilisateurs 
                    SET InvitationToken = @Token,
                        TokenExpiration = @Expiration
                    WHERE Id = @Id";

                using var updateCmd = new SqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@Token", newToken);
                updateCmd.Parameters.AddWithValue("@Expiration", newExpiration);
                updateCmd.Parameters.AddWithValue("@Id", utilisateurId);

                await updateCmd.ExecuteNonQueryAsync();

                // 4. Envoyer l'email
                string baseUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:3000";
                string callbackUrl = $"{baseUrl}/auth/complete-registration?token={newToken}";

                bool emailEnvoye = await _smtpEmailService.SendInvitationEmailAsync(
                    email,
                    $"{prenom} {nom}",
                    callbackUrl,
                    newExpiration
                );

                return new
                {
                    success = emailEnvoye,
                    message = emailEnvoye ? "Invitation renvoyée avec succès" : "Erreur lors de l'envoi de l'email"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    message = $"Erreur: {ex.Message}"
                };
            }
        }

        // ==================== Méthodes privées ====================

        private async Task<bool> EmailExisteAsync(string email)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "SELECT COUNT(*) FROM Utilisateurs WHERE Email = @Email";
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Email", email);

            int count = (int)await cmd.ExecuteScalarAsync();
            return count > 0;
        }

        private async Task<int> CreerUtilisateurInactifAsync(
            InviterUtilisateurRequest request,
            string token,
            DateTime tokenExpiration)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                INSERT INTO Utilisateurs (
                    Email, 
                    Prenom, 
                    Nom,
                    Username,
                    Telephone, 
                    RoleId,
                    Actif,
                    InvitationToken,
                    TokenExpiration,
                    DateCreation
                )
                VALUES (
                    @Email, 
                    @Prenom, 
                    @Nom,
                    @Prenom,
                    @Telephone, 
                    @RoleId,
                    0,
                    @Token,
                    @TokenExpiration,
                    GETDATE()
                );
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Email", request.Email);
            cmd.Parameters.AddWithValue("@Prenom", request.Prenom);
            cmd.Parameters.AddWithValue("@Nom", request.Nom);
            cmd.Parameters.AddWithValue("@Username", request.Prenom);
            cmd.Parameters.AddWithValue("@Telephone", (object)request.Telephone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RoleId", request.RoleId);
            cmd.Parameters.AddWithValue("@Token", token);
            cmd.Parameters.AddWithValue("@TokenExpiration", tokenExpiration);

            int utilisateurId = (int)await cmd.ExecuteScalarAsync();
            return utilisateurId;
        }

        private async Task SupprimerUtilisateurAsync(int utilisateurId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = "DELETE FROM Utilisateurs WHERE Id = @Id";
            using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Id", utilisateurId);

            await cmd.ExecuteNonQueryAsync();
        }

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
