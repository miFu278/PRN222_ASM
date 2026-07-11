namespace RAGChatBot.Domain.Models
{
    public sealed class QuizAttemptResult
    {
        public int Score { get; init; }
        public int TotalQuestions { get; init; }
        public double Percentage { get; init; }
        public DateTime AttemptedAt { get; init; }
    }
}
