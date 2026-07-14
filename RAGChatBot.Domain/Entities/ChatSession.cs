namespace RAGChatBot.Domain.Entities
{
    public class ChatSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateOnly UsageDate { get; set; }
        public int MessageCount { get; set; } = 0;
    }
}
