using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using System.Data;

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

        public async Task<IReadOnlyList<PaymentTransaction>> GetAllAsync(
            string? status = null,
            string? type = null)
        {
            var query = _db.PaymentTransactions
                .AsNoTracking()
                .Include(transaction => transaction.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(transaction => transaction.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(transaction => transaction.Type == type);
            }

            return await query
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PaymentTransaction>> GetByUserIdAsync(Guid userId)
        {
            return await _db.PaymentTransactions
                .AsNoTracking()
                .Include(transaction => transaction.User)
                .Where(transaction => transaction.UserId == userId)
                .OrderByDescending(transaction => transaction.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> CompletePaymentAsync(
            string orderId,
            long amount,
            string? transactionNo,
            Guid? expectedUserId = null)
        {
            // The row lock serializes callbacks for the same order. ReadCommitted avoids
            // PostgreSQL 40001 failures that Serializable can raise under webhook bursts.
            await using var transactionScope = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            var transaction = await _db.PaymentTransactions
                .FromSqlInterpolated($$"""
                    SELECT * FROM "PaymentTransactions"
                    WHERE "OrderId" = {{orderId}}
                    FOR UPDATE
                    """)
                .Include(item => item.User)
                .SingleOrDefaultAsync();

            if (transaction is null ||
                (expectedUserId.HasValue && transaction.UserId != expectedUserId.Value) ||
                transaction.Amount != amount)
            {
                return false;
            }

            if (string.Equals(transaction.Status, "Success", StringComparison.OrdinalIgnoreCase))
            {
                await transactionScope.CommitAsync();
                return true;
            }

            transaction.TransactionNo = transactionNo;
            transaction.Status = "Success";
            transaction.PaidAt = DateTime.UtcNow;

            var subscriptionStart = transaction.User.SubscriptionExpiresAt > DateTime.UtcNow
                ? transaction.User.SubscriptionExpiresAt.Value
                : DateTime.UtcNow;
            transaction.User.SubscriptionTier = "Premium";
            transaction.User.SubscriptionExpiresAt = subscriptionStart.AddMonths(1);

            await _db.SaveChangesAsync();
            await transactionScope.CommitAsync();
            return true;
        }

        public Task MarkFailedAsync(string orderId, Guid expectedUserId)
            => _db.PaymentTransactions
                .Where(transaction => transaction.OrderId == orderId &&
                    transaction.UserId == expectedUserId &&
                    transaction.Status == "Pending")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(transaction => transaction.Status, "Failed"));

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
