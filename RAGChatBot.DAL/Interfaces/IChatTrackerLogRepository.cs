using System.Threading.Tasks;
using RAGChatBot.DAL.Entities;

namespace RAGChatBot.DAL.Interfaces
{
    public interface IChatTrackerLogRepository
    {
        Task AddAsync(ChatTrackerLog log);
        Task SaveChangesAsync();
    }
}
