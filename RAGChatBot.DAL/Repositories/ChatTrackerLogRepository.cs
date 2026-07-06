using System.Threading.Tasks;
using RAGChatBot.DAL.Context;
using RAGChatBot.DAL.Entities;
using RAGChatBot.DAL.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class ChatTrackerLogRepository : IChatTrackerLogRepository
    {
        private readonly AppDbContext _db;
        public ChatTrackerLogRepository(AppDbContext db) => _db = db;

        public async Task AddAsync(ChatTrackerLog log)
        {
            // Do ChatTrackerLog chưa được cấu hình Migration/DbSet chính thức,
            // chúng ta trả về tác vụ đã hoàn tất để tránh lỗi runtime DB
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await Task.CompletedTask;
        }
    }
}
