using RAGChatBot.Domain.Models;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IDashboardRepository
    {
        Task<DashboardSummary> GetSummaryAsync();
        Task<IReadOnlyList<DashboardActivityPoint>> GetActivityAsync(int startYear, int endYear);
    }
}
