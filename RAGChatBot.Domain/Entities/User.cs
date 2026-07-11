using System;

namespace RAGChatBot.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public string SubscriptionTier { get; set; } = "Free"; // Free, Premium
        public string FullName { get; set; } = string.Empty;
        public DateTime? SubscriptionExpiresAt { get; set; }
    }
}
