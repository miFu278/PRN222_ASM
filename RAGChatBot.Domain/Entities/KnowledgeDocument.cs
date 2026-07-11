using RAGChatBot.Domain.Enums;

namespace RAGChatBot.Domain.Entities
{
    public class KnowledgeDocument
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string Chapter { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow.AddHours(7);
        public Guid UploadedBy { get; set; } // Reference to User.Id
        public string UploaderName { get; set; } = string.Empty; // Store name so it persists even if user is deleted
        public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
        public bool IsApproved { get; set; } = false; // Mặc định là chưa duyệt
        public string ChunkingStrategy { get; set; } = "Character"; // Character | Word | Paragraph
        public int ChunkSize { get; set; } = 500;
        public int Overlap { get; set; } = 50;

        // Liên kết một-nhiều tới các Chunks văn bản đã vector hóa
        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}
