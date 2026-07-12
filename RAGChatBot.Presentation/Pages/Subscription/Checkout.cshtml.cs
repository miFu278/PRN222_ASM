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
        private readonly IPayOSService _payOSService;
        private readonly IPaymentService _paymentService;

        public CheckoutModel(IPayOSService payOSService, IPaymentService paymentService)
        {
            _payOSService = payOSService;
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

            const long amount = 199000;

            // Tạo orderCode số dương unique cho PayOS (dùng timestamp, max 9 chữ số)
            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000;

            // Lưu vào DB với OrderId = orderCode (dạng string)
            await _paymentService.CreatePendingTransactionAsync(userId, amount, orderCode.ToString());

            var paymentUrl = await _payOSService.CreatePaymentUrl(orderCode, amount);
            return Redirect(paymentUrl);
        }
    }
}
