namespace RAGChatBot.Domain.Models
{
    public class KnowledgeDocument
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string Chapter { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public Guid UploadedBy { get; set; } // Reference to User.Id
        public bool IsProcessed { get; set; } = false; // Default is false ("Chờ xử lý")
    }
}
