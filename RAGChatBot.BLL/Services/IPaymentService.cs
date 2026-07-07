using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RAGChatBot.BLL.DTOs;

namespace RAGChatBot.BLL.Services
{
    public interface IPaymentService
    {
        Task<bool> ProcessPaymentCallbackAsync(VnPayCallbackResult callbackResult, Guid userId);
        Task<IEnumerable<PaymentTransactionDto>> GetAllTransactionsAsync();
    }
}
