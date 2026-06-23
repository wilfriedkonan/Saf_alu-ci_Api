using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Saf_alu_ci_Api.Controllers.Whatsapp
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class WhatsAppProxyController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<WhatsAppProxyController> _logger;

        public WhatsAppProxyController(
            HttpClient httpClient,
            IConfiguration config,
            ILogger<WhatsAppProxyController> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        // ── INSTANCES ────────────────────────────────
        [HttpGet("instances")]
        public async Task<IActionResult> GetAllInstances()
            => await ForwardRequest(HttpMethod.Get, "/api/Instances");

        [HttpPost("instances")]
        public async Task<IActionResult> CreateInstance([FromBody] object data)
            => await ForwardRequest(HttpMethod.Post, "/api/Instances", data);

        [HttpGet("instances/{instanceName}/status")]
        public async Task<IActionResult> GetStatus(string instanceName)
            => await ForwardRequest(HttpMethod.Get, $"/api/Instances/{instanceName}/status");

        [HttpGet("instances/{instanceName}/qrcode")]
        public async Task<IActionResult> GetQrCode(string instanceName)
            => await ForwardRequest(HttpMethod.Get, $"/api/Instances/{instanceName}/qrcode");

        [HttpPost("instances/{instanceName}/logout")]
        public async Task<IActionResult> Logout(string instanceName)
            => await ForwardRequest(HttpMethod.Post, $"/api/Instances/{instanceName}/logout");

        [HttpPost("instances/{instanceName}/restart")]
        public async Task<IActionResult> Restart(string instanceName)
            => await ForwardRequest(HttpMethod.Post, $"/api/Instances/{instanceName}/restart");

        [HttpDelete("instances/{instanceName}")]
        public async Task<IActionResult> DeleteInstance(string instanceName)
            => await ForwardRequest(HttpMethod.Delete, $"/api/Instances/{instanceName}");

        // ── MESSAGES ────────────────────────────────
        [HttpPost("messages/text")]
        public async Task<IActionResult> SendText([FromBody] object data)
            => await ForwardRequest(HttpMethod.Post, "/api/Messages/text", data);

        [HttpPost("messages/image")]
        public async Task<IActionResult> SendImage([FromBody] object data)
            => await ForwardRequest(HttpMethod.Post, "/api/Messages/image", data);

        [HttpPost("messages/document")]
        public async Task<IActionResult> SendDocument([FromBody] object data)
            => await ForwardRequest(HttpMethod.Post, "/api/Messages/document", data);

        [HttpPost("messages/audio")]
        public async Task<IActionResult> SendAudio([FromBody] object data)
            => await ForwardRequest(HttpMethod.Post, "/api/Messages/audio", data);

        [HttpPost("messages/buttons")]
        public async Task<IActionResult> SendButtons([FromBody] object data)
            => await ForwardRequest(HttpMethod.Post, "/api/Messages/buttons", data);

        // ── WEBHOOK ────────────────────────────────
        [HttpGet("webhook/ping")]
        public async Task<IActionResult> WebhookPing()
            => await ForwardRequest(HttpMethod.Get, "/api/Webhook/ping");

        [HttpPost("webhook")]
        public async Task<IActionResult> SendWebhook([FromBody] object data)
            => await ForwardRequest(HttpMethod.Post, "/api/Webhook", data);

        // ── PRIVATE METHOD ────────────────────────────
        private async Task<IActionResult> ForwardRequest(
            HttpMethod method,
            string endpoint,
            object? data = null)
        {
            try
            {
                // 1. Récupérer la clé API depuis appsettings (JAMAIS exposée!)
                var whatsappUrl = _config["WhatsApp:ServiceUrl"]
                    ?? throw new Exception("WhatsApp:ServiceUrl non configuré");
                var apiKey = _config["WhatsApp:ApiKey"]
                    ?? throw new Exception("WhatsApp:ApiKey non configuré");

                // 2. Créer la requête
                var url = $"{whatsappUrl}{endpoint}";
                var request = new HttpRequestMessage(method, url);

                // 3. Ajouter le contenu si POST/PUT
                if (data != null && (method == HttpMethod.Post || method == HttpMethod.Put))
                {
                    request.Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(data),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );
                }

                // 4. Ajouter la clé API au header (backend to backend)
                request.Headers.Add("X-Api-Key", apiKey);

                // 5. Envoyer la requête
                var response = await _httpClient.SendAsync(request);

                // 6. Lire la réponse
                var content = await response.Content.ReadAsStringAsync();

                // 7. Retourner au frontend avec enveloppe standard
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = System.Text.Json.JsonSerializer.Deserialize<object>(content);
                    return Ok(new { success = true, data = jsonData });
                }

                return StatusCode(
                    (int)response.StatusCode,
                    new { success = false, message = content }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur proxy WhatsApp");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}