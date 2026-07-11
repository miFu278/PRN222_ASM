using RAGChatBot.BLL.DTOs;

namespace RAGChatBot.BLL.Services
{
    public interface IChatService
    {
        Task<ChatReplyDto?> SendMessageAsync(
            Guid userId,
            string message,
            string? courseCode,
            Guid? threadId);

        Task<IReadOnlyList<ChatThreadDto>> GetThreadsAsync(Guid userId, string? courseCode);
        Task<IReadOnlyList<ChatMessageDto>?> GetMessagesAsync(Guid userId, Guid threadId);
    }
}
