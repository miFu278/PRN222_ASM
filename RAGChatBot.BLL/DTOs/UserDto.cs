using System;

namespace RAGChatBot.BLL.DTOs
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string SubscriptionTier { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime? SubscriptionExpiresAt { get; set; }
    }
}
