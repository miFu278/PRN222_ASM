namespace RAGChatBot.Domain.Models
{
    public sealed record DashboardActivityPoint(
        int Year,
        int Month,
        int DocumentCount,
        int ChatCount,
        decimal Revenue);
}
