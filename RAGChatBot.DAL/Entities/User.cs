namespace RAGChatBot.DAL.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Lecturer"; // Lecturer, Admin
        public string SubscriptionTier { get; set; } = "Free"; // Free, Premium
        public string FullName { get; set; } = string.Empty;
    }
}
