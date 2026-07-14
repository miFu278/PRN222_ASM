using RAGChatBot.BLL.DTOs;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public sealed class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _dashboardRepository;
        private readonly IBenchmarkRepository _benchmarkRepository;

        public DashboardService(
            IDashboardRepository dashboardRepository,
            IBenchmarkRepository benchmarkRepository)
        {
            _dashboardRepository = dashboardRepository;
            _benchmarkRepository = benchmarkRepository;
        }

        public async Task<DashboardStatsDto> GetStatsAsync(
            string period,
            int year,
            int? month,
            int? quarter)
        {
            var summary = await _dashboardRepository.GetSummaryAsync();
            var benchmarkAverages = await _benchmarkRepository.GetAveragesByTypeAsync();
            var recentBenchmarks = await _benchmarkRepository.GetRecentAsync(50);

            return new DashboardStatsDto
            {
                TotalUsers = summary.TotalUsers,
                PremiumUsers = summary.PremiumUsers,
                TotalDocuments = summary.TotalDocuments,
                TotalChatSessions = summary.TotalChatSessions,
                MonthlyRevenue = await GetRevenueChartAsync(period, year),
                BenchmarkAverages = benchmarkAverages
                    .Select(item => new BenchmarkAvgDto
                    {
                        OperationType = item.Key,
                        AvgMs = Math.Round(item.Value, 2)
                    })
                    .ToList(),
                RecentBenchmarks = recentBenchmarks
                    .Select(benchmark => new BenchmarkPointDto
                    {
                        OperationType = benchmark.OperationType,
                        DurationMs = benchmark.DurationMs,
                        DocumentName = benchmark.DocumentName,
                        MeasuredAt = benchmark.MeasuredAt
                    })
                    .ToList()
            };
        }

        public async Task<List<MonthlyRevenueDto>> GetRevenueChartAsync(string period, int year)
        {
            var normalizedPeriod = period?.Trim();
            var startYear = string.Equals(normalizedPeriod, "Year", StringComparison.OrdinalIgnoreCase)
                ? year - 2
                : year;
            var activity = await _dashboardRepository.GetActivityAsync(startYear, year);
            var result = new List<MonthlyRevenueDto>();

            if (string.Equals(normalizedPeriod, "Month", StringComparison.OrdinalIgnoreCase))
            {
                for (var month = 1; month <= 12; month++)
                {
                    result.Add(CreateRevenuePoint($"T{month}", activity
                        .Where(item => item.Year == year && item.Month == month)));
                }

                return result;
            }

            if (string.Equals(normalizedPeriod, "Quarter", StringComparison.OrdinalIgnoreCase))
            {
                for (var quarter = 1; quarter <= 4; quarter++)
                {
                    var startMonth = (quarter - 1) * 3 + 1;
                    var endMonth = startMonth + 2;
                    result.Add(CreateRevenuePoint($"Q{quarter}", activity
                        .Where(item => item.Year == year && item.Month >= startMonth && item.Month <= endMonth)));
                }

                return result;
            }

            for (var currentYear = year - 2; currentYear <= year; currentYear++)
            {
                result.Add(CreateRevenuePoint(currentYear.ToString(), activity
                    .Where(item => item.Year == currentYear)));
            }

            return result;
        }

        public async Task<List<BenchmarkAvgDto>> GetBenchmarkAveragesAsync()
        {
            var averages = await _benchmarkRepository.GetAveragesByTypeAsync();
            return averages
                .Select(item => new BenchmarkAvgDto
                {
                    OperationType = item.Key,
                    AvgMs = Math.Round(item.Value, 2)
                })
                .ToList();
        }

        public async Task<List<BenchmarkPointDto>> GetRecentBenchmarksAsync(int count = 50)
        {
            var benchmarks = await _benchmarkRepository.GetRecentAsync(count);
            return benchmarks
                .Select(benchmark => new BenchmarkPointDto
                {
                    OperationType = benchmark.OperationType,
                    DurationMs = benchmark.DurationMs,
                    DocumentName = benchmark.DocumentName,
                    MeasuredAt = benchmark.MeasuredAt
                })
                .ToList();
        }

        private static MonthlyRevenueDto CreateRevenuePoint(
            string label,
            IEnumerable<RAGChatBot.Domain.Models.DashboardActivityPoint> points)
        {
            var data = points.ToList();

            return new MonthlyRevenueDto
            {
                Label = label,
                Revenue = data.Sum(item => item.Revenue),
                DocumentCount = data.Sum(item => item.DocumentCount),
                ChatCount = data.Sum(item => item.ChatCount)
            };
        }
    }
}
