namespace RAGChatBot.BLL.DTOs
{
    public class CourseDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? SubjectLeaderId { get; set; }
        public string? SubjectLeaderName { get; set; }
    }
}
