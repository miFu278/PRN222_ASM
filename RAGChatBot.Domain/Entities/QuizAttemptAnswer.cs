namespace RAGChatBot.Domain.Entities
{
    public class QuizAttemptAnswer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AttemptId { get; set; }
        public QuizAttempt Attempt { get; set; } = null!;
        public Guid? QuestionId { get; set; }
        public int DisplayOrder { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public string? SelectedAnswer { get; set; }
        public bool? IsCorrect { get; set; }
    }
}
