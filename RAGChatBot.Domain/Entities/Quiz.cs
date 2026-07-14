using System;

namespace RAGChatBot.Domain.Entities
{
    public class Quiz
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public int QuestionCount { get; set; } = 20;
        public Guid? DocumentId { get; set; }
        public int MaxAttempts { get; set; } = 1;
        public int DurationMinutes { get; set; } = 30;
        public string? PasswordHash { get; set; }
        public QuizReviewPolicy ReviewPolicy { get; set; } = QuizReviewPolicy.ScoreOnly;
        public bool ShuffleQuestions { get; set; } = true;
        public bool ShuffleOptions { get; set; } = true;
        public bool IsPublished { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum QuizReviewPolicy
    {
        ScoreOnly = 0,
        OwnAnswers = 1,
        FullReview = 2
    }
}
