using RAGChatBot.Domain.Models;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IQuizEventService
    {
        Task NotifyQuizChangedAsync(RealtimeChangeEvent change, CancellationToken cancellationToken = default);
    }
}
