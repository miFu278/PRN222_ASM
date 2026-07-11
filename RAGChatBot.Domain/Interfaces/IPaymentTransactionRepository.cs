using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IPaymentTransactionRepository
    {
        Task AddAsync(PaymentTransaction transaction);
        Task<PaymentTransaction?> GetByOrderIdAsync(string orderId);
        Task<IReadOnlyList<PaymentTransaction>> GetAllAsync();
        Task SaveChangesAsync();
    }
}
