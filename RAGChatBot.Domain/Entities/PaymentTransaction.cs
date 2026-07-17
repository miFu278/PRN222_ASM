using RAGChatBot.Domain.Constants;

namespace RAGChatBot.Domain.Entities
{
    public class PaymentTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OrderId { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public long Amount { get; set; }
        public string? TransactionNo { get; set; }
        public string Type { get; set; } = PaymentTransactionTypes.PremiumSubscription;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
    }
}
