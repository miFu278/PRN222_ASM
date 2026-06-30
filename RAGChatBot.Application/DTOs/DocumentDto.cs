using System;

using RAGChatBot.Domain.Enums;

namespace RAGChatBot.Application.DTOs
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
    }
}
