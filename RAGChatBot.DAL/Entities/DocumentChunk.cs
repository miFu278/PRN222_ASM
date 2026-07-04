namespace RAGChatBot.DAL.Entities
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public KnowledgeDocument Document { get; set; } = null!;
        public string Content { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }

        // Kiểu dữ liệu Vector của pgvector (sẽ sử dụng lớp Vector của thư viện Pgvector)
        public Pgvector.Vector? Embedding { get; set; }
    }
}
