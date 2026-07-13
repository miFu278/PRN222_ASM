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
        private static long _lastOrderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private readonly IPayOSService _payOSService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<CheckoutModel> _logger;

        public CheckoutModel(IPayOSService payOSService, IPaymentService paymentService, ILogger<CheckoutModel> logger)
        {
            _payOSService = payOSService;
            _paymentService = paymentService;
            _logger = logger;
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

            var orderCode = Interlocked.Increment(ref _lastOrderCode);

            // Lưu vào DB với OrderId = orderCode (dạng string)
            await _paymentService.CreatePendingTransactionAsync(userId, amount, orderCode.ToString());

            try
            {
                var paymentUrl = await _payOSService.CreatePaymentUrl(orderCode, amount);
                return Redirect(paymentUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS CreatePaymentUrl failed for orderCode {OrderCode}", orderCode);
                await _paymentService.CancelTransactionAsync(orderCode.ToString(), userId);
                ErrorMessage = "Không thể khởi tạo thanh toán PayOS. Vui lòng thử lại.";
                return Page();
            }
        }
    }
}
