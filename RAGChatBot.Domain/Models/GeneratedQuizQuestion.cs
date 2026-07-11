namespace RAGChatBot.Domain.Models
{
    public sealed class GeneratedQuizQuestion
    {
        public string Question { get; init; } = string.Empty;
        public string OptionA { get; init; } = string.Empty;
        public string OptionB { get; init; } = string.Empty;
        public string OptionC { get; init; } = string.Empty;
        public string OptionD { get; init; } = string.Empty;
        public string CorrectAnswer { get; init; } = string.Empty;
    }
}
