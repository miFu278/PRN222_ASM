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
        private readonly IAuthService _authService;
        private readonly ILogger<CheckoutModel> _logger;

        public CheckoutModel(
            IPayOSService payOSService,
            IPaymentService paymentService,
            IAuthService authService,
            ILogger<CheckoutModel> logger)
        {
            _payOSService = payOSService;
            _paymentService = paymentService;
            _authService = authService;
            _logger = logger;
        }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!TryGetUserId(out var userId))
            {
                return Challenge();
            }

            if (await HasActivePremiumAsync(userId))
            {
                TempData["SuccessMessage"] = "Bạn đã ở gói Premium rồi, không cần đăng ký thêm!";
                return RedirectToPage("/Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!TryGetUserId(out var userId))
            {
                ErrorMessage = "Nhận diện người dùng không hợp lệ.";
                return Page();
            }

            if (await HasActivePremiumAsync(userId))
            {
                return RedirectToPage("/Index");
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
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "PayOS payment initialization failed for OrderCode={OrderCode}, UserId={UserId}",
                    orderCode,
                    userId);

                try
                {
                    await _paymentService.CancelTransactionAsync(orderCode.ToString(), userId);
                }
                catch (Exception cancellationException)
                {
                    _logger.LogError(
                        cancellationException,
                        "Could not mark failed PayOS transaction for OrderCode={OrderCode}",
                        orderCode);
                }

                ErrorMessage = "Không thể khởi tạo thanh toán PayOS. Vui lòng thử lại.";
                return Page();
            }
        }

        private bool TryGetUserId(out Guid userId)
            => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        private async Task<bool> HasActivePremiumAsync(Guid userId)
        {
            var user = await _authService.GetUserByIdAsync(userId);
            return user is not null &&
                string.Equals(user.SubscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase) &&
                (!user.SubscriptionExpiresAt.HasValue || user.SubscriptionExpiresAt.Value > DateTime.UtcNow);
        }
    }
}
