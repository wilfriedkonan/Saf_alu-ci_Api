using System.Text;
using System.Text.Json;

namespace Saf_alu_ci_Api.Services.messagerie
{
    public class MailServiceBrevo
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public MailServiceBrevo(IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _apiKey = configuration["Brevo:ApiKey"]; // stocke la clé dans appsettings.json
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string toName, string otpCode)
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
                background-color: #FF3B30;
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
            <p>Merci de votre inscription. Voici votre code de vérification :</p>
            <p class='otp-code'>{otpCode}</p>
            <p>Ce code est valide pour une durée limitée. Veuillez ne pas le partager avec d'autres personnes.</p>
            <p>Merci,<br>L'équipe Salizo</p>
            <div class='footer'>
                &copy; 2025 Salizo. Tous droits réservés.
            </div>
        </div>
    </body>
    </html>";

            var emailData = new
            {
                sender = new
                {
                    name = "Support Salizo",
                    email = "salizo.ohio@gmail.com"
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

            var content = new StringContent(JsonSerializer.Serialize(emailData), Encoding.UTF8, "application/json");

            if (!_httpClient.DefaultRequestHeaders.Contains("api-key"))
            {
                _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            }

            var response = await _httpClient.PostAsync("https://api.brevo.com/v3/smtp/email", content);
            return response.IsSuccessStatusCode;
        }
    }
}
