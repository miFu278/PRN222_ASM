using System;

namespace RAGChatBot.Domain.Entities
{
    public class QuizAttempt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public double Percentage { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    }
}
