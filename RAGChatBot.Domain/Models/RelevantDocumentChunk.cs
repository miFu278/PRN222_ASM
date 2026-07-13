namespace RAGChatBot.Domain.Models
{
    public sealed class RelevantDocumentChunk
    {
        public Guid DocumentId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public int ChunkIndex { get; init; }
        public string Content { get; init; } = string.Empty;
        public double Distance { get; init; }
    }
}
