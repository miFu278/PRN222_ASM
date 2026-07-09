namespace RAGChatBot.BLL.DTOs
{
    public class DashboardStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalDocuments { get; set; }
        public int TotalChatSessions { get; set; }
        public int PremiumUsers { get; set; }
        public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
        public List<BenchmarkAvgDto> BenchmarkAverages { get; set; } = new();
        public List<BenchmarkPointDto> RecentBenchmarks { get; set; } = new();
    }

    public class MonthlyRevenueDto
    {
        public string Label { get; set; } = string.Empty; // "T1", "Q1", "2025"
        public decimal Revenue { get; set; }
        public int DocumentCount { get; set; }
        public int ChatCount { get; set; }
    }

    public class BenchmarkAvgDto
    {
        public string OperationType { get; set; } = string.Empty;
        public double AvgMs { get; set; }
    }

    public class BenchmarkPointDto
    {
        public string OperationType { get; set; } = string.Empty;
        public double DurationMs { get; set; }
        public string? DocumentName { get; set; }
        public DateTime MeasuredAt { get; set; }
    }

}
