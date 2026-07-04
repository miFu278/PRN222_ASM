using System;

namespace RAGChatBot.Application.BusinessEntities
{
    public class WhitelistEmailDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? StudentId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
