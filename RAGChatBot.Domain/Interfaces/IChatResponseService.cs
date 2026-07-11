namespace RAGChatBot.Domain.Interfaces
{
    public interface IChatResponseService
    {
        /// <summary>
        /// Generates a RAG response using the supplied conversation history.
        /// </summary>
        Task<string> GetChatResponseAsync(
            string question,
            string? courseCode,
            IReadOnlyList<ChatHistoryItem> history);
    }

    public sealed record ChatHistoryItem(string Role, string Content);
}
