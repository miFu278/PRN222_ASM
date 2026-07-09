using Microsoft.EntityFrameworkCore;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.DAL.Context;
using RAGChatBot.DAL.Interfaces;
using System.Diagnostics;

namespace RAGChatBot.BLL.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _db;
        private readonly IBenchmarkRepository _benchmarkRepo;
        private const decimal PremiumMonthlyPrice = 199000m; // VND

        public DashboardService(AppDbContext db, IBenchmarkRepository benchmarkRepo)
        {
            _db = db;
            _benchmarkRepo = benchmarkRepo;
        }

        public async Task<DashboardStatsDto> GetStatsAsync(
            string period, int year, int? month, int? quarter)
        {
            var totalUsers = await _db.Users.CountAsync();
            var premiumUsers = await _db.Users.CountAsync(u => u.SubscriptionTier == "Premium");
            var totalDocs = await _db.KnowledgeDocuments.CountAsync();
            var totalChats = await _db.ChatSessions.CountAsync();

            var benchmarkAvgs = await _benchmarkRepo.GetAveragesByTypeAsync();
            var recentBenchmarks = await _benchmarkRepo.GetRecentAsync(50);

            return new DashboardStatsDto
            {
                TotalUsers = totalUsers,
                PremiumUsers = premiumUsers,
                TotalDocuments = totalDocs,
                TotalChatSessions = totalChats,
                MonthlyRevenue = await GetRevenueChartAsync(period, year),
                BenchmarkAverages = benchmarkAvgs
                    .Select(kv => new BenchmarkAvgDto
                    {
                        OperationType = kv.Key,
                        AvgMs = Math.Round(kv.Value, 2)
                    }).ToList(),
                RecentBenchmarks = recentBenchmarks
                    .Select(b => new BenchmarkPointDto
                    {
                        OperationType = b.OperationType,
                        DurationMs = b.DurationMs,
                        DocumentName = b.DocumentName,
                        MeasuredAt = b.MeasuredAt
                    }).ToList()
            };
        }

        public async Task<List<MonthlyRevenueDto>> GetRevenueChartAsync(string period, int year)
        {
            var result = new List<MonthlyRevenueDto>();

            // Lấy tất cả người dùng Premium có ngày hết hạn gói cước thực tế từ DB
            var premiumUsers = await _db.Users
                .Where(u => u.SubscriptionTier == "Premium" && u.SubscriptionExpiresAt != null)
                .ToListAsync();

            if (period == "Month")
            {
                for (int m = 1; m <= 12; m++)
                {
                    var docs = await _db.KnowledgeDocuments
                        .CountAsync(d => d.UploadedAt.Year == year && d.UploadedAt.Month == m);
                    var chats = await _db.ChatSessions
                        .CountAsync(c => c.CreatedAt.Year == year && c.CreatedAt.Month == m);
                    
                    // Tính doanh thu thực tế: dựa vào ngày hết hạn lùi đi 1 tháng (thời điểm mua gói cước 1 tháng)
                    decimal revenue = 0m;
                    foreach (var u in premiumUsers)
                    {
                        var paymentDate = u.SubscriptionExpiresAt!.Value.AddMonths(-1);
                        if (paymentDate.Year == year && paymentDate.Month == m)
                        {
                            revenue += PremiumMonthlyPrice;
                        }
                    }

                    result.Add(new MonthlyRevenueDto
                    {
                        Label = $"T{m}",
                        Revenue = revenue,
                        DocumentCount = docs,
                        ChatCount = chats
                    });
                }
            }
            else if (period == "Quarter")
            {
                for (int q = 1; q <= 4; q++)
                {
                    var sm = (q - 1) * 3 + 1;
                    var em = sm + 2;
                    var docs = await _db.KnowledgeDocuments.CountAsync(d =>
                        d.UploadedAt.Year == year &&
                        d.UploadedAt.Month >= sm && d.UploadedAt.Month <= em);
                    var chats = await _db.ChatSessions.CountAsync(c =>
                        c.CreatedAt.Year == year &&
                        c.CreatedAt.Month >= sm && c.CreatedAt.Month <= em);

                    decimal revenue = 0m;
                    foreach (var u in premiumUsers)
                    {
                        var paymentDate = u.SubscriptionExpiresAt!.Value.AddMonths(-1);
                        if (paymentDate.Year == year && paymentDate.Month >= sm && paymentDate.Month <= em)
                        {
                            revenue += PremiumMonthlyPrice;
                        }
                    }

                    result.Add(new MonthlyRevenueDto
                    {
                        Label = $"Q{q}",
                        Revenue = revenue,
                        DocumentCount = docs,
                        ChatCount = chats
                    });
                }
            }
            else // Year
            {
                for (int y = year - 2; y <= year; y++)
                {
                    var docs = await _db.KnowledgeDocuments
                        .CountAsync(d => d.UploadedAt.Year == y);
                    var chats = await _db.ChatSessions.CountAsync(c => c.CreatedAt.Year == y);

                    decimal revenue = 0m;
                    foreach (var u in premiumUsers)
                    {
                        var paymentDate = u.SubscriptionExpiresAt!.Value.AddMonths(-1);
                        if (paymentDate.Year == y)
                        {
                            revenue += PremiumMonthlyPrice;
                        }
                    }

                    result.Add(new MonthlyRevenueDto
                    {
                        Label = $"{y}",
                        Revenue = revenue,
                        DocumentCount = docs,
                        ChatCount = chats
                    });
                }
            }

            return result;
        }

        public async Task<List<BenchmarkAvgDto>> GetBenchmarkAveragesAsync()
        {
            var avgs = await _benchmarkRepo.GetAveragesByTypeAsync();
            return avgs.Select(kv => new BenchmarkAvgDto
            {
                OperationType = kv.Key,
                AvgMs = Math.Round(kv.Value, 2)
            }).ToList();
        }

        public async Task<List<BenchmarkPointDto>> GetRecentBenchmarksAsync(int count = 50)
        {
            var items = await _benchmarkRepo.GetRecentAsync(count);
            return items.Select(b => new BenchmarkPointDto
            {
                OperationType = b.OperationType,
                DurationMs = b.DurationMs,
                DocumentName = b.DocumentName,
                MeasuredAt = b.MeasuredAt
            }).ToList();
        }

    }
}
