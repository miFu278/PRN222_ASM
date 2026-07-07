using RAGChatBot.DAL.Context;
using RAGChatBot.DAL.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly AppDbContext _db;
        public PaymentTransactionRepository(AppDbContext db) => _db = db;
    }
}
