using RAGChatBot.BLL.DTOs;

namespace RAGChatBot.BLL.Services
{
    public interface IDashboardService
    {
        Task<DashboardStatsDto> GetStatsAsync(string period, int year, int? month, int? quarter);
        Task<List<MonthlyRevenueDto>> GetRevenueChartAsync(string period, int year);
        Task<List<BenchmarkAvgDto>> GetBenchmarkAveragesAsync();
        Task<List<BenchmarkPointDto>> GetRecentBenchmarksAsync(int count = 50);
    }
}
