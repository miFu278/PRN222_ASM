using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly AppDbContext _db;
        public PaymentTransactionRepository(AppDbContext db) => _db = db;

        public async Task AddAsync(PaymentTransaction transaction)
        {
            await _db.PaymentTransactions.AddAsync(transaction);
        }

        public async Task<PaymentTransaction?> GetByOrderIdAsync(string orderId)
        {
            return await _db.PaymentTransactions
                .Include(transaction => transaction.User)
                .FirstOrDefaultAsync(transaction => transaction.OrderId == orderId);
        }

        public async Task<IReadOnlyList<PaymentTransaction>> GetAllAsync()
        {
            return await _db.PaymentTransactions
                .AsNoTracking()
                .Include(transaction => transaction.User)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
