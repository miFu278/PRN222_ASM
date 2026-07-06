using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.DAL.Entities;
using RAGChatBot.DAL.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class ChatSessionRepository : IChatSessionRepository
    {
        private readonly AppDbContext _db;
        public ChatSessionRepository(AppDbContext db) => _db = db;

        public async Task<int> CountByMonthAsync(int year, int month)
            => await _db.ChatSessions.CountAsync(c =>
                c.CreatedAt.Year == year && c.CreatedAt.Month == month);

        public async Task<int> CountByQuarterAsync(int year, int quarter)
        {
            var startMonth = (quarter - 1) * 3 + 1;
            var endMonth = startMonth + 2;
            return await _db.ChatSessions.CountAsync(c =>
                c.CreatedAt.Year == year &&
                c.CreatedAt.Month >= startMonth &&
                c.CreatedAt.Month <= endMonth);
        }

        public async Task<int> CountByYearAsync(int year)
            => await _db.ChatSessions.CountAsync(c => c.CreatedAt.Year == year);

        public async Task IncrementAsync(Guid userId, string courseCode)
        {
            var session = await _db.ChatSessions
                .FirstOrDefaultAsync(c => c.UserId == userId &&
                    c.CreatedAt.Date == DateTime.UtcNow.Date);
            if (session == null)
            {
                _db.ChatSessions.Add(new ChatSession
                {
                    UserId = userId,
                    CourseCode = courseCode,
                    MessageCount = 1
                });
            }
            else
            {
                session.MessageCount++;
            }
            await _db.SaveChangesAsync();
        }
    }
}
