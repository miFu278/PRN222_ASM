using RAGChatBot.Domain.Models;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IDashboardRepository
    {
        Task<DashboardSummary> GetSummaryAsync();
        Task<IReadOnlyList<DateTime>> GetPremiumSubscriptionExpiryDatesAsync();
        Task<int> CountDocumentsAsync(int year, int? startMonth = null, int? endMonth = null);
        Task<int> CountChatSessionsAsync(int year, int? startMonth = null, int? endMonth = null);
        Task<decimal> GetRevenueAsync(int year, int? startMonth = null, int? endMonth = null);
    }
}
