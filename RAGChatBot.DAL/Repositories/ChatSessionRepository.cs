using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class ChatSessionRepository : IChatSessionRepository
    {
        private readonly AppDbContext _db;
        public ChatSessionRepository(AppDbContext db) => _db = db;

        public async Task<(bool Allowed, int Remaining)> TryConsumeDailyCreditAsync(
            Guid userId,
            string courseCode,
            DateOnly usageDate,
            int dailyLimit)
        {
            var counts = await _db.Database.SqlQuery<int>($$"""
                INSERT INTO "ChatSessions"
                    ("Id", "UserId", "CourseCode", "CreatedAt", "UsageDate", "MessageCount")
                VALUES
                    ({{Guid.NewGuid()}}, {{userId}}, {{courseCode}}, {{DateTime.UtcNow}}, {{usageDate}}, 1)
                ON CONFLICT ("UserId", "UsageDate")
                DO UPDATE SET
                    "MessageCount" = "ChatSessions"."MessageCount" + 1,
                    "CourseCode" = EXCLUDED."CourseCode"
                WHERE "ChatSessions"."MessageCount" < {{dailyLimit}}
                RETURNING "MessageCount" AS "Value"
                """).ToListAsync();

            if (counts.Count == 0)
            {
                return (false, 0);
            }

            return (true, Math.Max(0, dailyLimit - counts[0]));
        }

        public Task RefundDailyCreditAsync(Guid userId, DateOnly usageDate)
            => _db.ChatSessions
                .Where(session => session.UserId == userId &&
                    session.UsageDate == usageDate &&
                    session.MessageCount > 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(session => session.MessageCount, session => session.MessageCount - 1));
    }
}
