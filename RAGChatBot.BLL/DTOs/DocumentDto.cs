using System;

using RAGChatBot.DAL.Enums;

namespace RAGChatBot.BLL.DTOs
{
    public class DocumentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string Chapter { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public Guid UploadedBy { get; set; }
        public DocumentStatus Status { get; set; }
        public bool IsApproved { get; set; }
        public string? UploaderName { get; set; }
        public string ChunkingStrategy { get; set; } = "Character";
        public int ChunkSize { get; set; } = 500;
        public int Overlap { get; set; } = 50;
    }
}
