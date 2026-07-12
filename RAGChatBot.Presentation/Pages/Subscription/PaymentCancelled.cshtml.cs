using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Subscription
{
    [Authorize]
    public class PaymentCancelledModel : PageModel
    {
        private readonly IPaymentService _paymentService;

        public PaymentCancelledModel(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // PayOS trả về orderCode qua query string khi user hủy
            var orderCode = Request.Query["orderCode"].ToString();
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(orderCode) && Guid.TryParse(userIdValue, out var userId))
            {
                await _paymentService.CancelTransactionAsync(orderCode, userId);
            }

            return Page();
        }
    }
}
