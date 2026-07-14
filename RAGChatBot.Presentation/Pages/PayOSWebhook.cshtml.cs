using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PayOS;
using PayOS.Models.Webhooks;
using RAGChatBot.BLL.Services;

namespace RAGChatBot.Presentation.Pages
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public sealed class PayOSWebhookModel : PageModel
    {
        private readonly PayOSClient _payOSClient;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PayOSWebhookModel> _logger;

        public PayOSWebhookModel(
            PayOSClient payOSClient,
            IPaymentService paymentService,
            ILogger<PayOSWebhookModel> logger)
        {
            _payOSClient = payOSClient;
            _paymentService = paymentService;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync([FromBody] Webhook? webhook)
        {
            if (webhook is null)
            {
                return BadRequest(new { error = "Webhook payload is required." });
            }

            try
            {
                var verified = await _payOSClient.Webhooks.VerifyAsync(webhook);
                var processed = await _paymentService.ProcessVerifiedPaymentAsync(
                    verified.OrderCode.ToString(),
                    verified.Amount,
                    verified.Reference ?? verified.PaymentLinkId);

                if (!processed)
                {
                    _logger.LogWarning(
                        "Verified PayOS webhook did not match a pending order. OrderCode={OrderCode}",
                        verified.OrderCode);
                }

                // A valid signature is acknowledged even for PayOS' webhook validation sample.
                return new JsonResult(new { success = true });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Rejected invalid PayOS webhook.");
                return BadRequest(new { error = "Invalid webhook." });
            }
        }
    }
}
