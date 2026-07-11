namespace RAGChatBot.BLL.DTOs
{
    public sealed class ChatReplyDto
    {
        public string Reply { get; init; } = string.Empty;
        public int Remaining { get; init; }
        public Guid? ThreadId { get; init; }
        public bool OutOfCredits { get; init; }
    }

    public sealed class ChatThreadDto
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }

    public sealed class ChatMessageDto
    {
        public Guid Id { get; init; }
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public DateTime SentAt { get; init; }
    }
}
