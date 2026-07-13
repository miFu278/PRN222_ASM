namespace RAGChatBot.Domain.Interfaces
{
    public interface IChatResponseService
    {
        /// <summary>
        /// Generates a RAG response using the supplied conversation history.
        /// </summary>
        Task<ChatResponseResult> GetChatResponseAsync(
            string question,
            string courseCode,
            IReadOnlyList<ChatHistoryItem> history);
    }

    public sealed record ChatHistoryItem(string Role, string Content);

    public sealed record ChatSource(
        Guid DocumentId,
        string FileName,
        string CourseCode,
        int ChunkIndex,
        double Distance);

    public sealed record ChatResponseResult(
        string Reply,
        bool IsSuccessful,
        IReadOnlyList<ChatSource> Sources);
}
