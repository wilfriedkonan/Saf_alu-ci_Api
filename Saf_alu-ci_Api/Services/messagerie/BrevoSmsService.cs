using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Saf_alu_ci_Api.Services.messagerie
{
    public class BrevoSmsService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;

        public BrevoSmsService(HttpClient client, IConfiguration config)
        {
            _client = client;
            _apiKey = config["Brevo:ApiKey"];

            _client.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public async Task<bool> SendSmsAsync(string phone, string message)
        {
            try
            {
                var payload = new
                {
                    sender = "SAF-ALU",
                    recipient = phone,
                    content = message
                };

                var json = JsonSerializer.Serialize(payload);

                var response = await _client.PostAsync(
                    "https://api.brevo.com/v3/transactionalSMS/sms",
                    new StringContent(json, Encoding.UTF8, "application/json")
                );
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("BREVO RESPONSE: " + responseContent);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Erreur SMS: " + err);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur SMS: " + ex.Message);
                return false;
            }
        }
    }
}
