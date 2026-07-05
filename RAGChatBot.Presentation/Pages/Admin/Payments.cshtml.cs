using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;

namespace RAGChatBot.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class PaymentsModel : PageModel
    {
        private readonly IPaymentService _paymentService;

        public PaymentsModel(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public IEnumerable<PaymentTransactionDto> Transactions { get; set; } = new List<PaymentTransactionDto>();

        public async Task OnGetAsync()
        {
            Transactions = await _paymentService.GetAllTransactionsAsync();
        }
    }
}
