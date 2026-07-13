using RAGChatBot.Domain.Models;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IDocumentEventService
    {
        Task NotifyDocumentChangedAsync(RealtimeChangeEvent change, CancellationToken cancellationToken = default);
    }
}
