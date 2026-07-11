using System.Threading.Tasks;
using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IChatTrackerLogRepository
    {
        Task AddAsync(ChatTrackerLog log);
        Task SaveChangesAsync();
    }
}
