using System;

namespace RAGChatBot.Domain.Entities
{
    public class QuizAttempt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        // Nullable only for legacy attempts created before quizzes were introduced.
        public Guid? QuizId { get; set; }
        public int AttemptNumber { get; set; }
        public QuizAttemptStatus Status { get; set; } = QuizAttemptStatus.InProgress;
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public double Percentage { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
        public string? QuizTitle { get; set; }
        public QuizReviewPolicy ReviewPolicy { get; set; } = QuizReviewPolicy.ScoreOnly;
        public ICollection<QuizAttemptAnswer> Answers { get; set; } = new List<QuizAttemptAnswer>();
    }

    public enum QuizAttemptStatus
    {
        InProgress = 0,
        Submitted = 1
    }
}
