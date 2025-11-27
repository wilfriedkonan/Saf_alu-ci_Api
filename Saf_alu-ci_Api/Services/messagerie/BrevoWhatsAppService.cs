using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Saf_alu_ci_Api.Services.messagerie
{
    public class BrevoWhatsAppService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;

        public BrevoWhatsAppService(HttpClient client, IConfiguration config)
        {
            _client = client;
            _apiKey = config["Brevo:ApiKey"];

            _client.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public async Task<bool> SendWhatsAppMessageAsync(
            string phone,
            long templateId,
            Dictionary<string, string>? parameters = null)
        {
            try
            {
                var payload = new
                {
                    to = phone,
                    templateId = templateId,
                    parameters = parameters
                };

                var json = JsonSerializer.Serialize(payload);

                var response = await _client.PostAsync(
                    "https://api.brevo.com/v1/whatsapp/sendMessage",
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur WhatsApp : {ex.Message}");
                return false;
            }
        }
    }
}