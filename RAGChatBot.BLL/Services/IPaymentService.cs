using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RAGChatBot.BLL.DTOs;

namespace RAGChatBot.BLL.Services
{
    public interface IPaymentService
    {
        Task<string> CreatePendingTransactionAsync(Guid userId, long amount, string? orderId = null);
        Task<bool> ProcessPaymentCallbackAsync(PayOSCallbackResult callbackResult, Guid userId);
        Task<bool> ProcessVerifiedPaymentAsync(string orderId, long amount, string? transactionNo);
        Task<IEnumerable<PaymentTransactionDto>> GetAllTransactionsAsync();
        Task<IEnumerable<PaymentTransactionDto>> GetTransactionsByUserAsync(Guid userId);
        Task CancelTransactionAsync(string orderId, Guid userId);
    }
}
