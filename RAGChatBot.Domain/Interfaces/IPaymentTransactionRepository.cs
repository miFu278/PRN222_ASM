using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IPaymentTransactionRepository
    {
        Task AddAsync(PaymentTransaction transaction);
        Task<PaymentTransaction?> GetByOrderIdAsync(string orderId);
        Task<IReadOnlyList<PaymentTransaction>> GetAllAsync(
            string? status = null,
            string? type = null);
        Task<IReadOnlyList<PaymentTransaction>> GetByUserIdAsync(Guid userId);
        Task<bool> CompletePaymentAsync(
            string orderId,
            long amount,
            string? transactionNo,
            Guid? expectedUserId = null);
        Task MarkFailedAsync(string orderId, Guid expectedUserId);
        Task SaveChangesAsync();
    }
}
