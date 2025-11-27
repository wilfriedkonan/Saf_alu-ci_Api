using Microsoft.AspNetCore.Mvc;
using Saf_alu_ci_Api.Services.messagerie;

namespace Saf_alu_ci_Api.Controllers.Messagerie
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : BaseController
    {
        private readonly BrevoWhatsAppService _whatsapp;
        private readonly BrevoSmsService _brevoSmsService;

        public NotificationController(
            BrevoWhatsAppService whatsapp,
            BrevoSmsService brevoSmsService)
        {
            _whatsapp = whatsapp;
            _brevoSmsService = brevoSmsService;
        }

        // ------------------------------------------------------------
        // 🚀 Envoi WhatsApp
        // ------------------------------------------------------------
        [HttpPost("whatsapp/send")]
        public async Task<IActionResult> SendWhatsApp([FromBody] SendWhatsAppRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Phone))
                return BadRequest("Numéro invalide");

            if (req.TemplateId <= 0)
                return BadRequest("TemplateId invalide");

            var success = await _whatsapp.SendWhatsAppMessageAsync(
                phone: req.Phone,
                templateId: req.TemplateId,
                //senderNumber: req.SenderNumber,
                parameters: req.Parameters
            );

            return success
                ? Ok(new { message = "Message envoyé avec succès" })
                : StatusCode(500, new { message = "Erreur durant l’envoi WhatsApp" });
        }

        // ------------------------------------------------------------
        // 📩 Envoi SMS
        // ------------------------------------------------------------
        [HttpPost("sms/send")]
        public async Task<IActionResult> SendSms([FromBody] SmsRequest req)
        {
            var result = await _brevoSmsService.SendSmsAsync(req.Phone, req.Message);

            return Ok(new
            {
                success = result,
                req.Phone,
                req.Message
            });
        }

        public class SmsRequest
        {
            public string Phone { get; set; }
            public string Message { get; set; }
        }


        // ------------------------------------------------------------
        // DTO WhatsApp
        // ------------------------------------------------------------
        public class SendWhatsAppRequest
        {
            public string Phone { get; set; }
            public long TemplateId { get; set; }
            public string? SenderNumber { get; set; }
            public Dictionary<string, string>? Parameters { get; set; }
        }

        // ------------------------------------------------------------
        // DTO SMS
        // ------------------------------------------------------------
        public class SendSmsRequest
        {
            public string Phone { get; set; }
            public string Message { get; set; }
        }
    }
}