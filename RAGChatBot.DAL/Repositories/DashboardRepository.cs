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
                PremiumUsers = await _db.Users.CountAsync(user =>
                    user.SubscriptionTier == "Premium" &&
                    (!user.SubscriptionExpiresAt.HasValue || user.SubscriptionExpiresAt > DateTime.UtcNow)),
                TotalDocuments = await _db.KnowledgeDocuments.CountAsync(),
                TotalChatSessions = await _db.ChatThreads.CountAsync()
            };

        public async Task<IReadOnlyList<DashboardActivityPoint>> GetActivityAsync(
            int startYear,
            int endYear)
        {
            var documents = await _db.KnowledgeDocuments
                .AsNoTracking()
                .Where(document => document.UploadedAt.Year >= startYear && document.UploadedAt.Year <= endYear)
                .GroupBy(document => new { document.UploadedAt.Year, document.UploadedAt.Month })
                .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
                .ToListAsync();

            var chats = await _db.ChatThreads
                .AsNoTracking()
                .Where(thread => thread.CreatedAt.Year >= startYear && thread.CreatedAt.Year <= endYear)
                .GroupBy(thread => new { thread.CreatedAt.Year, thread.CreatedAt.Month })
                .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
                .ToListAsync();

            var revenue = await _db.PaymentTransactions
                .AsNoTracking()
                .Where(transaction => transaction.Status == "Success" &&
                    transaction.PaidAt.HasValue &&
                    transaction.PaidAt.Value.Year >= startYear &&
                    transaction.PaidAt.Value.Year <= endYear)
                .GroupBy(transaction => new
                {
                    transaction.PaidAt!.Value.Year,
                    transaction.PaidAt.Value.Month
                })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    Total = group.Sum(transaction => transaction.Amount)
                })
                .ToListAsync();

            var keys = documents.Select(item => (item.Year, item.Month))
                .Concat(chats.Select(item => (item.Year, item.Month)))
                .Concat(revenue.Select(item => (item.Year, item.Month)))
                .Distinct()
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Month);

            return keys.Select(key => new DashboardActivityPoint(
                key.Year,
                key.Month,
                documents.FirstOrDefault(item => item.Year == key.Year && item.Month == key.Month)?.Count ?? 0,
                chats.FirstOrDefault(item => item.Year == key.Year && item.Month == key.Month)?.Count ?? 0,
                revenue.FirstOrDefault(item => item.Year == key.Year && item.Month == key.Month)?.Total ?? 0L))
                .ToList();
        }
    }
}
