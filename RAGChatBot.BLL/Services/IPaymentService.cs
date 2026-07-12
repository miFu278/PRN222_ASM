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
        Task CancelTransactionAsync(string orderId);
        Task<IEnumerable<PaymentTransactionDto>> GetAllTransactionsAsync();
    }
}
