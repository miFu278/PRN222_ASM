namespace RAGChatBot.Domain.Models
{
    public sealed class QuizQuestionModel
    {
        public Guid Id { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public string OptionA { get; init; } = string.Empty;
        public string OptionB { get; init; } = string.Empty;
        public string OptionC { get; init; } = string.Empty;
        public string OptionD { get; init; } = string.Empty;
    }
}
