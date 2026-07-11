using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;

namespace RAGChatBot.DAL.Repositories
{
    public sealed class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _db;

        public DashboardRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardSummary> GetSummaryAsync()
            => new()
            {
                TotalUsers = await _db.Users.CountAsync(),
                PremiumUsers = await _db.Users.CountAsync(user => user.SubscriptionTier == "Premium"),
                TotalDocuments = await _db.KnowledgeDocuments.CountAsync(),
                TotalChatSessions = await _db.ChatSessions.CountAsync()
            };

        public async Task<IReadOnlyList<DateTime>> GetPremiumSubscriptionExpiryDatesAsync()
            => await _db.Users
                .AsNoTracking()
                .Where(user =>
                    user.SubscriptionTier == "Premium" &&
                    user.SubscriptionExpiresAt.HasValue)
                .Select(user => user.SubscriptionExpiresAt!.Value)
                .ToListAsync();

        public Task<int> CountDocumentsAsync(
            int year,
            int? startMonth = null,
            int? endMonth = null)
            => _db.KnowledgeDocuments.CountAsync(document =>
                document.UploadedAt.Year == year &&
                (!startMonth.HasValue || document.UploadedAt.Month >= startMonth.Value) &&
                (!endMonth.HasValue || document.UploadedAt.Month <= endMonth.Value));

        public Task<int> CountChatSessionsAsync(
            int year,
            int? startMonth = null,
            int? endMonth = null)
            => _db.ChatSessions.CountAsync(session =>
                session.CreatedAt.Year == year &&
                (!startMonth.HasValue || session.CreatedAt.Month >= startMonth.Value) &&
                (!endMonth.HasValue || session.CreatedAt.Month <= endMonth.Value));

        public async Task<decimal> GetRevenueAsync(
            int year,
            int? startMonth = null,
            int? endMonth = null)
        {
            var query = _db.PaymentTransactions
                .Where(t => t.Status == "Success" &&
                            t.PaidAt.HasValue &&
                            t.PaidAt.Value.Year == year &&
                            (!startMonth.HasValue || t.PaidAt.Value.Month >= startMonth.Value) &&
                            (!endMonth.HasValue || t.PaidAt.Value.Month <= endMonth.Value));

            var total = await query.SumAsync(t => (long?)t.Amount) ?? 0L;
            return (decimal)total;
        }
    }
}
