using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Subscription
{
    [Authorize]
    public class PaymentCallbackModel : PageModel
    {
        private readonly IVnPayService _vnPayService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentCallbackModel> _logger;

        public PaymentCallbackModel(IVnPayService vnPayService, IPaymentService paymentService, ILogger<PaymentCallbackModel> logger)
        {
            _vnPayService = vnPayService;
            _paymentService = paymentService;
            _logger = logger;
        }

        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TransactionNo { get; set; }
        public long Amount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var callbackParameters = Request.Query.ToDictionary(
                item => item.Key,
                item => item.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
            var callbackResult = _vnPayService.ValidateCallback(callbackParameters);

            _logger.LogInformation("[VNPay Callback] OrderId={OrderId}, ResponseCode={ResponseCode}, Valid={IsValid}, Success={IsSuccess}",
                callbackResult.OrderId, callbackResult.ResponseCode, callbackResult.IsValid, callbackResult.IsSuccess);

            if (!callbackResult.IsValid)
            {
                IsSuccess = false;
                Message = "Chữ ký giao dịch không hợp lệ. Vui lòng liên hệ hỗ trợ.";
                return Page();
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                IsSuccess = false;
                Message = "Phiên đăng nhập không hợp lệ.";
                return Page();
            }

            var success = await _paymentService.ProcessPaymentCallbackAsync(callbackResult, userId);

            IsSuccess = success;
            Message = callbackResult.Message;
            TransactionNo = callbackResult.TransactionNo;
            Amount = callbackResult.Amount;

            // Nếu thành công → cập nhật claims sang Premium
            if (success)
            {
                var currentClaims = User.Claims.ToList();
                var tierClaim = currentClaims.FirstOrDefault(c => c.Type == "SubscriptionTier");
                if (tierClaim != null)
                {
                    currentClaims.Remove(tierClaim);
                }
                currentClaims.Add(new Claim("SubscriptionTier", "Premium"));

                var claimsIdentity = new ClaimsIdentity(currentClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);
            }

            return Page();
        }
    }
}
