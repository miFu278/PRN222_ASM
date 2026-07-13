namespace RAGChatBot.Domain.Models
{
    public sealed class RealtimeChangeEvent
    {
        public string Type { get; init; } = string.Empty;
        public string? CourseCode { get; init; }
        public Guid? EntityId { get; init; }
        public string? Status { get; init; }
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }
}
