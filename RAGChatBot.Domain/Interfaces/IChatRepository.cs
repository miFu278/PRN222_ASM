using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IChatRepository
    {
        Task<ChatThread?> GetThreadForUserAsync(Guid threadId, Guid userId);
        Task<IReadOnlyList<ChatThread>> GetThreadsByUserAsync(Guid userId, string? courseCode);
        Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid threadId);
        Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid threadId, int count);
        Task<ChatThread> CreateThreadAsync(Guid userId, string courseCode, string title, DateTime createdAt);
        Task AddExchangeAsync(
            Guid threadId,
            string userMessage,
            string assistantMessage,
            DateTime sentAt);
    }
}
