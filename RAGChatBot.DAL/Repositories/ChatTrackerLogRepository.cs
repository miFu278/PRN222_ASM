using System.Threading.Tasks;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class ChatTrackerLogRepository : IChatTrackerLogRepository
    {
        private readonly AppDbContext _db;
        public ChatTrackerLogRepository(AppDbContext db) => _db = db;

        public async Task AddAsync(ChatTrackerLog log)
        {
            await _db.ChatTrackerLogs.AddAsync(log);
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
