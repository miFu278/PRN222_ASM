using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Models
{
    public sealed class QuizSummaryModel
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public int QuestionCount { get; init; }
        public Guid? DocumentId { get; init; }
        public int MaxAttempts { get; init; }
        public int DurationMinutes { get; init; }
        public int AttemptsUsed { get; init; }
        public bool HasPassword { get; init; }
        public QuizReviewPolicy ReviewPolicy { get; init; }
        public bool ShuffleQuestions { get; init; }
        public bool ShuffleOptions { get; init; }
        public bool IsPublished { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public sealed class QuizStartResult
    {
        public Guid AttemptId { get; init; }
        public int AttemptNumber { get; init; }
        public DateTime ExpiresAt { get; init; }
        public IReadOnlyList<QuizQuestionModel> Questions { get; init; } = Array.Empty<QuizQuestionModel>();
    }

    public sealed class QuizReviewModel
    {
        public Guid AttemptId { get; init; }
        public Guid QuizId { get; init; }
        public string CourseCode { get; init; } = string.Empty;
        public string QuizTitle { get; init; } = string.Empty;
        public int AttemptNumber { get; init; }
        public int Score { get; init; }
        public int TotalQuestions { get; init; }
        public double Percentage { get; init; }
        public DateTime? SubmittedAt { get; init; }
        public QuizReviewPolicy ReviewPolicy { get; init; }
        public IReadOnlyList<QuizReviewQuestionModel> Questions { get; init; } = Array.Empty<QuizReviewQuestionModel>();
    }

    public sealed class QuizReviewQuestionModel
    {
        public int DisplayOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public string OptionA { get; init; } = string.Empty;
        public string OptionB { get; init; } = string.Empty;
        public string OptionC { get; init; } = string.Empty;
        public string OptionD { get; init; } = string.Empty;
        public string? SelectedAnswer { get; init; }
        public string? CorrectAnswer { get; init; }
        public bool? IsCorrect { get; init; }
    }
}
