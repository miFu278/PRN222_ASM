using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Subscription
{
    [Authorize]
    public class CheckoutModel : PageModel
    {
        private readonly IVnPayService _vnPayService;
        private readonly IPaymentService _paymentService;

        public CheckoutModel(IVnPayService vnPayService, IPaymentService paymentService)
        {
            _vnPayService = vnPayService;
            _paymentService = paymentService;
        }

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            var userTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";
            if (userTier == "Premium")
            {
                TempData["SuccessMessage"] = "Bạn đã ở gói Premium rồi, không cần đăng ký thêm!";
                return RedirectToPage("/Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";
            if (userTier == "Premium")
            {
                return RedirectToPage("/Index");
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                ErrorMessage = "Nhận diện người dùng không hợp lệ.";
                return Page();
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
            const long amount = 199000;
            var orderId = await _paymentService.CreatePendingTransactionAsync(userId, amount);
            var paymentUrl = _vnPayService.CreatePaymentUrl(userId, ipAddress, orderId);

            return Redirect(paymentUrl);
        }
    }
}
