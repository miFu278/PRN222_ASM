using System;

namespace RAGChatBot.Domain.Models
{
    public class QuizAttemptDetailsDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentUsername { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public double Percentage { get; set; }
        public DateTime AttemptedAt { get; set; }
        public string? QuizTitle { get; set; }
        public Guid QuizId { get; set; }
        public int AttemptNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
    }
}
