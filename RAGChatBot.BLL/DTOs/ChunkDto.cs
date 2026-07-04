namespace RAGChatBot.BLL.DTOs
{
    public class ChunkDto
    {
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool HasEmbedding { get; set; }
    }
}
