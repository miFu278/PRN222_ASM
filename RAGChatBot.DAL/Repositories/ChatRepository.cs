using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly AppDbContext _db;

        public ChatRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<ChatThread?> GetThreadForUserAsync(Guid threadId, Guid userId)
            => _db.ChatThreads
                .AsNoTracking()
                .FirstOrDefaultAsync(thread => thread.Id == threadId && thread.UserId == userId);

        public async Task<IReadOnlyList<ChatThread>> GetThreadsByUserAsync(Guid userId, string? courseCode)
        {
            var query = _db.ChatThreads
                .AsNoTracking()
                .Where(thread => thread.UserId == userId);

            if (!string.IsNullOrWhiteSpace(courseCode))
            {
                var normalizedCourseCode = courseCode.Trim().ToLower();
                query = query.Where(thread => thread.CourseCode.ToLower() == normalizedCourseCode);
            }

            return await query
                .OrderByDescending(thread => thread.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid threadId)
            => await _db.ChatMessages
                .AsNoTracking()
                .Where(message => message.ThreadId == threadId)
                .OrderBy(message => message.SentAt)
                .ToListAsync();

        public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(Guid threadId, int count)
        {
            var messages = await _db.ChatMessages
                .AsNoTracking()
                .Where(message => message.ThreadId == threadId)
                .OrderByDescending(message => message.SentAt)
                .Take(count)
                .ToListAsync();

            messages.Reverse();
            return messages;
        }

        public async Task<ChatThread> CreateThreadAsync(
            Guid userId,
            string courseCode,
            string title,
            DateTime createdAt)
        {
            var thread = new ChatThread
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CourseCode = courseCode,
                Title = title,
                CreatedAt = createdAt
            };

            _db.ChatThreads.Add(thread);
            await _db.SaveChangesAsync();
            return thread;
        }

        public async Task AddExchangeAsync(
            Guid threadId,
            string userMessage,
            string assistantMessage,
            DateTime sentAt)
        {
            _db.ChatMessages.AddRange(
                new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ThreadId = threadId,
                    Role = "user",
                    Content = userMessage,
                    SentAt = sentAt
                },
                new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ThreadId = threadId,
                    Role = "assistant",
                    Content = assistantMessage,
                    SentAt = sentAt.AddTicks(1)
                });

            await _db.SaveChangesAsync();
        }
    }
}
