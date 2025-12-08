using System.Text;
using System.Text.Json;

namespace Saf_alu_ci_Api.Services.messagerie
{
    public class MailServiceBrevo
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _senderEmail;
        private readonly string _senderName;

        public MailServiceBrevo(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["Brevo:ApiKey"];
            _senderEmail = configuration["Brevo:SenderEmail"] ?? "wilfried.konan34@gmail.com";
            _senderName = configuration["Brevo:SenderName"] ?? "SAF ALU-CI";
        }

        /// <summary>
        /// Envoie un email d'invitation pour compléter l'inscription
        /// </summary>
        public async Task<bool> SendInvitationEmailAsync(
            string toEmail,
            string toName,
            string callbackUrl,
            DateTime expirationDate)
        {
            var htmlContent = $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Invitation SAF ALU-CI</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f7f9;
            margin: 0;
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 40px auto;
            background-color: #ffffff;
            border-radius: 12px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            padding: 40px 30px;
            text-align: center;
            color: #ffffff;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content p {{
            color: #333333;
            line-height: 1.6;
            margin: 0 0 15px 0;
        }}
        .cta-button {{
            display: inline-block;
            margin: 30px 0;
            padding: 16px 40px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: #ffffff;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            transition: all 0.3s ease;
        }}
        .cta-button:hover {{
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(102, 126, 234, 0.4);
        }}
        .info-box {{
            background-color: #f8f9fa;
            border-left: 4px solid #667eea;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .info-box p {{
            margin: 0;
            color: #555;
            font-size: 14px;
        }}
        .footer {{
            background-color: #f8f9fa;
            padding: 20px 30px;
            text-align: center;
            color: #666;
            font-size: 13px;
        }}
        .footer a {{
            color: #667eea;
            text-decoration: none;
        }}
        .steps {{
            margin: 20px 0;
        }}
        .step {{
            display: flex;
            align-items: flex-start;
            margin: 15px 0;
        }}
        .step-number {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            width: 30px;
            height: 30px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            flex-shrink: 0;
            margin-right: 15px;
        }}
        .step-content {{
            flex: 1;
            padding-top: 5px;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>🎉 Bienvenue chez SAF ALU-CI</h1>
        </div>
        
        <div class='content'>
            <p>Bonjour <strong>{toName}</strong>,</p>
            
            <p>Vous avez été invité(e) à rejoindre la plateforme SAF ALU-CI ! Nous sommes ravis de vous accueillir dans notre équipe.</p>
            
            <div class='info-box'>
                <p><strong>📧 Email:</strong> {toEmail}</p>
                <p><strong>⏰ Lien valide jusqu'au:</strong> {expirationDate:dd/MM/yyyy à HH:mm}</p>
            </div>
            
            <p><strong>Pour compléter votre inscription, suivez ces étapes :</strong></p>
            
            <div class='steps'>
                <div class='step'>
                    <div class='step-number'>1</div>
                    <div class='step-content'>
                        Cliquez sur le bouton ci-dessous pour accéder à la page d'inscription
                    </div>
                </div>
                <div class='step'>
                    <div class='step-number'>2</div>
                    <div class='step-content'>
                        Choisissez un nom d'utilisateur unique
                    </div>
                </div>
                <div class='step'>
                    <div class='step-number'>3</div>
                    <div class='step-content'>
                        Créez un mot de passe sécurisé (minimum 8 caractères)
                    </div>
                </div>
                <div class='step'>
                    <div class='step-number'>4</div>
                    <div class='step-content'>
                        Confirmez votre mot de passe et validez
                    </div>
                </div>
            </div>
            
            <div style='text-align: center;'>
                <a href='{callbackUrl}' class='cta-button'>
                    ✨ Compléter mon inscription
                </a>
            </div>
            
            <p style='margin-top: 30px; color: #666; font-size: 14px;'>
                Si le bouton ne fonctionne pas, copiez et collez ce lien dans votre navigateur :<br>
                <a href='{callbackUrl}' style='color: #667eea; word-break: break-all;'>{callbackUrl}</a>
            </p>
            
            <div class='info-box' style='margin-top: 30px;'>
                <p><strong>⚠️ Important :</strong></p>
                <p>• Ce lien est valable pendant 48 heures</p>
                <p>• Si vous n'avez pas demandé cette invitation, ignorez cet email</p>
                <p>• Ne partagez jamais ce lien avec d'autres personnes</p>
            </div>
        </div>
        
        <div class='footer'>
            <p>Si vous avez des questions, contactez-nous à <a href='mailto:support@safalu.ci'>support@safalu.ci</a></p>
            <p style='margin-top: 10px;'>&copy; 2025 SAF ALU-CI. Tous droits réservés.</p>
            <p style='margin-top: 5px;'> Riviera Triangle, Abidjan, Côte d'Ivoire</p>
        </div>
    </div>
</body>
</html>";

            var emailData = new
            {
                sender = new
                {
                    name = _senderName,
                    email = _senderEmail
                },
                to = new[] {
                    new {
                        email = toEmail,
                        name = toName
                    }
                },
                subject = "🎉 Complétez votre inscription à SAF ALU-CI",
                htmlContent = htmlContent
            };

            return await SendEmailAsync(emailData);
        }

        /// <summary>
        /// Envoie un email OTP (conservé pour compatibilité)
        /// </summary>
        public async Task<bool> SendOtpEmailAsync(string toEmail, string toName, string otpCode, string url = "")
        {
            var htmlContent = $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <title>Code OTP</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f4f4;
            padding: 20px;
        }}
        .email-container {{
            max-width: 600px;
            margin: auto;
            background-color: #ffffff;
            padding: 30px;
            border-radius: 10px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
        }}
        .otp-code {{
            font-size: 24px;
            color: #ffffff;
            background-color: #667eea;
            display: inline-block;
            padding: 10px 20px;
            border-radius: 5px;
            letter-spacing: 2px;
            margin: 10px 0;
        }}
        .footer {{
            font-size: 12px;
            color: #888;
            margin-top: 30px;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <p>Bonjour <strong>{toName}</strong>,</p>
        <p>Voici votre code de vérification :</p>
        <p class='otp-code'>{otpCode}</p>
        <p>Ce code est valide pour une durée limitée. Veuillez ne pas le partager avec d'autres personnes.</p>
        <p>Merci,<br>L'équipe SAF ALU-CI</p>
        <div class='footer'>
            &copy; 2025 SAF ALU-CI. Tous droits réservés.
        </div>
    </div>
</body>
</html>";

            var emailData = new
            {
                sender = new
                {
                    name = _senderName,
                    email = _senderEmail
                },
                to = new[] {
                    new {
                        email = toEmail,
                        name = toName
                    }
                },
                subject = "Votre code OTP de confirmation",
                htmlContent = htmlContent
            };

            return await SendEmailAsync(emailData);
        }


        /// <summary>
        /// Envoie un email de réinitialisation de mot de passe
        /// </summary>
        public async Task<bool> SendResetPasswordEmailAsync(
            string toEmail,
            string toName,
            string resetUrl,
            DateTime expirationDate)
        {
            var htmlContent = $@"
<!DOCTYPE html>
<html lang='fr'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Réinitialisation mot de passe</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f7f9;
            margin: 0;
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 40px auto;
            background-color: #ffffff;
            border-radius: 12px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.1);
            overflow: hidden;
        }}
        .header {{
            background: linear-gradient(135deg, #f43f5e 0%, #e11d48 100%);
            padding: 40px 30px;
            text-align: center;
            color: #ffffff;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }}
        .header-icon {{
            font-size: 48px;
            margin-bottom: 10px;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content p {{
            color: #333333;
            line-height: 1.6;
            margin: 0 0 15px 0;
        }}
        .cta-button {{
            display: inline-block;
            margin: 30px 0;
            padding: 16px 40px;
            background: linear-gradient(135deg, #f43f5e 0%, #e11d48 100%);
            color: #ffffff;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            transition: all 0.3s ease;
        }}
        .cta-button:hover {{
            transform: translateY(-2px);
            box-shadow: 0 5px 15px rgba(244, 63, 94, 0.4);
        }}
        .info-box {{
            background-color: #fef2f2;
            border-left: 4px solid #f43f5e;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .info-box p {{
            margin: 5px 0;
            color: #555;
            font-size: 14px;
        }}
        .warning-box {{
            background-color: #fff7ed;
            border-left: 4px solid #f97316;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .warning-box p {{
            margin: 5px 0;
            color: #555;
            font-size: 14px;
        }}
        .footer {{
            background-color: #f8f9fa;
            padding: 20px 30px;
            text-align: center;
            color: #666;
            font-size: 13px;
        }}
        .footer a {{
            color: #f43f5e;
            text-decoration: none;
        }}
        .security-tips {{
            margin: 20px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
        }}
        .security-tips h3 {{
            margin: 0 0 10px 0;
            color: #333;
            font-size: 16px;
        }}
        .security-tips ul {{
            margin: 0;
            padding-left: 20px;
            color: #666;
            font-size: 14px;
        }}
        .security-tips li {{
            margin: 5px 0;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <div class='header-icon'>🔒</div>
            <h1>Réinitialisation de mot de passe</h1>
        </div>
        
        <div class='content'>
            <p>Bonjour <strong>{toName}</strong>,</p>
            
            <p>Vous avez demandé à réinitialiser votre mot de passe sur la plateforme SAF ALU-CI.</p>
            
            <div class='info-box'>
                <p><strong>📧 Email:</strong> {toEmail}</p>
                <p><strong>⏰ Lien valide jusqu'au:</strong> {expirationDate:dd/MM/yyyy à HH:mm} (1 heure)</p>
            </div>
            
            <p><strong>Pour définir un nouveau mot de passe, cliquez sur le bouton ci-dessous :</strong></p>
            
            <div style='text-align: center;'>
                <a href='{resetUrl}' class='cta-button'>
                    🔐 Réinitialiser mon mot de passe
                </a>
            </div>
            
            <p style='margin-top: 30px; color: #666; font-size: 14px;'>
                Si le bouton ne fonctionne pas, copiez et collez ce lien dans votre navigateur :<br>
                <a href='{resetUrl}' style='color: #f43f5e; word-break: break-all;'>{resetUrl}</a>
            </p>
            
            <div class='warning-box'>
                <p><strong>⚠️ Vous n'avez pas demandé cette réinitialisation ?</strong></p>
                <p>Si vous n'êtes pas à l'origine de cette demande, ignorez cet email. Votre mot de passe actuel reste inchangé.</p>
                <p>Nous vous recommandons de changer votre mot de passe immédiatement si vous pensez que quelqu'un tente d'accéder à votre compte.</p>
            </div>
            
            <div class='security-tips'>
                <h3>💡 Conseils pour un mot de passe sécurisé :</h3>
                <ul>
                    <li>Minimum 8 caractères</li>
                    <li>Mélangez majuscules et minuscules</li>
                    <li>Incluez des chiffres et caractères spéciaux</li>
                    <li>Évitez les informations personnelles</li>
                    <li>N'utilisez pas le même mot de passe sur plusieurs sites</li>
                </ul>
            </div>
        </div>
        
        <div class='footer'>
            <p>Besoin d'aide ? Contactez-nous à <a href='mailto:support@safalu.ci'>support@safalu.ci</a></p>
            <p style='margin-top: 10px;'>&copy; 2025 SAF ALU-CI. Tous droits réservés.</p>
            <p style='margin-top: 5px;'>Riviera Triangle, Abidjan, Côte d'Ivoire</p>
        </div>
    </div>
</body>
</html>";

            var emailData = new
            {
                sender = new
                {
                    name = _senderName,
                    email = _senderEmail
                },
                to = new[] {
                    new {
                        email = toEmail,
                        name = toName
                    }
                },
                subject = "🔒 Réinitialisation de votre mot de passe - SAF ALU-CI",
                htmlContent = htmlContent
            };

            return await SendEmailAsync(emailData);
        }

        /// <summary>
        /// Méthode générique d'envoi d'email
        /// </summary>
        private async Task<bool> SendEmailAsync(object emailData)
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(emailData),
                    Encoding.UTF8,
                    "application/json"
                );

                if (!_httpClient.DefaultRequestHeaders.Contains("api-key"))
                {
                    _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
                }

                var response = await _httpClient.PostAsync(
                    "https://api.brevo.com/v3/smtp/email",
                    content
                );

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // Log l'erreur (vous pouvez utiliser ILogger)
                Console.WriteLine($"Erreur envoi email: {ex.Message}");
                return false;
            }
        }
    }
}