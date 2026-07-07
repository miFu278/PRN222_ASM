using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;

namespace RAGChatBot.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DashboardModel : PageModel
    {
        private readonly IDashboardService _dashboard;

        public DashboardModel(IDashboardService dashboard)
        {
            _dashboard = dashboard;
        }

        // Bind properties cho filter form
        [BindProperty(SupportsGet = true)]
        public string Period { get; set; } = "Month";

        [BindProperty(SupportsGet = true)]
        public int Year { get; set; } = DateTime.Now.Year;

        // Data properties cho view
        public DashboardStatsDto Stats { get; set; } = new();
        public List<MonthlyRevenueDto> RevenueChart { get; set; } = new();
        public List<BenchmarkAvgDto> BenchmarkAverages { get; set; } = new();
        public List<BenchmarkPointDto> RecentBenchmarks { get; set; } = new();
        public List<GitContributorDto> GitContributions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Stats = await _dashboard.GetStatsAsync(Period, Year, null, null);
            RevenueChart = await _dashboard.GetRevenueChartAsync(Period, Year);
            BenchmarkAverages = await _dashboard.GetBenchmarkAveragesAsync();
            RecentBenchmarks = await _dashboard.GetRecentBenchmarksAsync(50);
            GitContributions = await _dashboard.GetGitContributionsAsync();

            return Page();
        }

        // API endpoint cho AJAX filter (đổi period/year mà không reload page)
        public async Task<IActionResult> OnGetRevenueDataAsync(string period, int year)
        {
            var data = await _dashboard.GetRevenueChartAsync(period, year);
            return new JsonResult(data);
        }

        public async Task<IActionResult> OnGetStatsDataAsync(string period, int year)
        {
            var stats = await _dashboard.GetStatsAsync(period, year, null, null);
            return new JsonResult(stats);
        }
    }
}
