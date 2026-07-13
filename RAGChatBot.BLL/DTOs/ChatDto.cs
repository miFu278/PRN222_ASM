namespace RAGChatBot.BLL.DTOs
{
    public sealed class ChatReplyDto
    {
        public string Reply { get; init; } = string.Empty;
        public int Remaining { get; init; }
        public Guid? ThreadId { get; init; }
        public bool OutOfCredits { get; init; }
        public bool IsError { get; init; }
        public IReadOnlyList<ChatSourceDto> Sources { get; init; } = Array.Empty<ChatSourceDto>();
    }

    public sealed class ChatSourceDto
    {
        public Guid DocumentId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public int ChunkIndex { get; init; }
        public double Relevance { get; init; }
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
