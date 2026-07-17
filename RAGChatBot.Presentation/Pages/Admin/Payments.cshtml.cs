using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Domain.Constants;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;

namespace RAGChatBot.Presentation.Pages.Admin
{
    [Authorize(Roles = RoleNames.Admin)]
    public class PaymentsModel : PageModel
    {
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Success",
            "Pending",
            "Failed"
        };

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            PaymentTransactionTypes.PremiumSubscription
        };

        private readonly IPaymentService _paymentService;

        public PaymentsModel(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        public IEnumerable<PaymentTransactionDto> Transactions { get; set; } = new List<PaymentTransactionDto>();

        [BindProperty(SupportsGet = true)]
        public string Status { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public string Type { get; set; } = "All";

        public async Task OnGetAsync()
        {
            var selectedStatus = AllowedStatuses.FirstOrDefault(
                status => status.Equals(Status?.Trim(), StringComparison.OrdinalIgnoreCase));
            var selectedType = AllowedTypes.FirstOrDefault(
                type => type.Equals(Type?.Trim(), StringComparison.OrdinalIgnoreCase));

            Status = selectedStatus ?? "All";
            Type = selectedType ?? "All";
            Transactions = await _paymentService.GetAllTransactionsAsync(selectedStatus, selectedType);
        }
    }
}
