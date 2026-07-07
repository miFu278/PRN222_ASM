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

            if (period == "Month")
            {
                for (int m = 1; m <= 12; m++)
                {
                    var docs = await _db.KnowledgeDocuments
                        .CountAsync(d => d.UploadedAt.Year == year && d.UploadedAt.Month == m);
                    var chats = await _db.ChatSessions
                        .CountAsync(c => c.CreatedAt.Year == year && c.CreatedAt.Month == m);
                    var premium = await _db.Users
                        .CountAsync(u => u.SubscriptionTier == "Premium");
                    result.Add(new MonthlyRevenueDto
                    {
                        Label = $"T{m}",
                        Revenue = premium * PremiumMonthlyPrice,
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
                    result.Add(new MonthlyRevenueDto
                    {
                        Label = $"Q{q}",
                        Revenue = chats * PremiumMonthlyPrice / 3,
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
                    result.Add(new MonthlyRevenueDto
                    {
                        Label = $"{y}",
                        Revenue = chats * PremiumMonthlyPrice / 12,
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

        public async Task<List<GitContributorDto>> GetGitContributionsAsync()
        {
            var repoPath = Directory.GetCurrentDirectory();
            var contributors = new List<GitContributorDto>();

            try
            {
                // Lấy danh sách tác giả và số commit
                var logOutput = await RunGitCommandAsync(
                    "shortlog -sn --all --no-merges", repoPath);

                foreach (var line in logOutput.Split('\n',
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split('\t');
                    if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int commits))
                    {
                        contributors.Add(new GitContributorDto
                        {
                            Author = parts[1].Trim(),
                            CommitCount = commits
                        });
                    }
                }

                // Lấy thống kê dòng code cho mỗi tác giả
                foreach (var contributor in contributors)
                {
                    var statOutput = await RunGitCommandAsync(
                        $"log --author=\"{contributor.Author}\" --pretty=tformat: --numstat --all",
                        repoPath);

                    int added = 0, deleted = 0;
                    foreach (var line in statOutput.Split('\n',
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = line.Split('\t');
                        if (parts.Length >= 2 &&
                            int.TryParse(parts[0], out int a) &&
                            int.TryParse(parts[1], out int d))
                        {
                            added += a;
                            deleted += d;
                        }
                    }
                    contributor.LinesAdded = added;
                    contributor.LinesDeleted = deleted;
                }
            }
            catch
            {
                contributors.Add(new GitContributorDto
                {
                    Author = "Git không khả dụng",
                    CommitCount = 0
                });
            }

            return contributors;
        }

        private static async Task<string> RunGitCommandAsync(string args, string workDir)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
    }
}
