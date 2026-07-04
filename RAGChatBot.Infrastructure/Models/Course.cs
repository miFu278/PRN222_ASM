namespace RAGChatBot.Infrastructure.Models
{
    public class Course
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid CreatedBy { get; set; } // Reference to User.Id
        public Guid? SubjectLeaderId { get; set; } // Reference to User.Id (Trưởng bộ môn)
    }
}
