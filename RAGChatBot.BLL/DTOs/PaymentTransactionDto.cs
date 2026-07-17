using System;

namespace RAGChatBot.BLL.DTOs
{
    public class PaymentTransactionDto
    {
        public string OrderId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string? TransactionNo { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
