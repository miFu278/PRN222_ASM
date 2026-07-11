using RAGChatBot.BLL.DTOs;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public sealed class DashboardService : IDashboardService
    {
        private const decimal PremiumMonthlyPrice = 199000m;
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
            var expiryDates = await _dashboardRepository.GetPremiumSubscriptionExpiryDatesAsync();
            var result = new List<MonthlyRevenueDto>();

            if (period == "Month")
            {
                for (var month = 1; month <= 12; month++)
                {
                    result.Add(await CreateRevenuePointAsync(
                        $"T{month}",
                        year,
                        month,
                        month,
                        expiryDates));
                }

                return result;
            }

            if (period == "Quarter")
            {
                for (var quarter = 1; quarter <= 4; quarter++)
                {
                    var startMonth = (quarter - 1) * 3 + 1;
                    result.Add(await CreateRevenuePointAsync(
                        $"Q{quarter}",
                        year,
                        startMonth,
                        startMonth + 2,
                        expiryDates));
                }

                return result;
            }

            for (var currentYear = year - 2; currentYear <= year; currentYear++)
            {
                result.Add(await CreateRevenuePointAsync(
                    currentYear.ToString(),
                    currentYear,
                    null,
                    null,
                    expiryDates));
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

        private async Task<MonthlyRevenueDto> CreateRevenuePointAsync(
            string label,
            int year,
            int? startMonth,
            int? endMonth,
            IReadOnlyList<DateTime> expiryDates)
        {
            var documentCount = await _dashboardRepository.CountDocumentsAsync(
                year,
                startMonth,
                endMonth);
            var chatCount = await _dashboardRepository.CountChatSessionsAsync(
                year,
                startMonth,
                endMonth);

            var paymentCount = expiryDates.Count(expiryDate =>
            {
                var paymentDate = expiryDate.AddMonths(-1);
                return paymentDate.Year == year &&
                       (!startMonth.HasValue || paymentDate.Month >= startMonth.Value) &&
                       (!endMonth.HasValue || paymentDate.Month <= endMonth.Value);
            });

            return new MonthlyRevenueDto
            {
                Label = label,
                Revenue = paymentCount * PremiumMonthlyPrice,
                DocumentCount = documentCount,
                ChatCount = chatCount
            };
        }
    }
}
