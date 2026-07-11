namespace RAGChatBot.Domain.Models
{
    public sealed class DashboardSummary
    {
        public int TotalUsers { get; init; }
        public int PremiumUsers { get; init; }
        public int TotalDocuments { get; init; }
        public int TotalChatSessions { get; init; }
    }
}
