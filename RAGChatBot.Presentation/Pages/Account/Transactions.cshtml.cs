using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Account
{
    [Authorize]
    public class TransactionsModel : PageModel
    {
        private readonly IPaymentService _paymentService;

        public TransactionsModel(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public IReadOnlyList<PaymentTransactionDto> Transactions { get; private set; }
            = Array.Empty<PaymentTransactionDto>();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Challenge();
            }

            Transactions = (await _paymentService.GetTransactionsByUserAsync(userId)).ToList();
            return Page();
        }
    }
}
