using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.DAL.Repositories
{
    public class CourseRepository : ICourseRepository
    {
        private readonly AppDbContext _context;

        public CourseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Course> AddAsync(Course course)
        {
            await _context.Courses.AddAsync(course);
            await _context.SaveChangesAsync();
            return course;
        }

        public async Task<IEnumerable<Course>> GetAllAsync()
        {
            return await _context.Courses
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Course>> SearchAsync(string keyword)
        {
            var keywordLower = keyword.ToLower();
            return await _context.Courses
                .Where(c => c.Code.ToLower().Contains(keywordLower) || c.Name.ToLower().Contains(keywordLower))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Course?> GetByIdAsync(System.Guid id)
        {
            return await _context.Courses.FindAsync(id);
        }

        public Task<Course?> GetByCodeAsync(string code)
        {
            var normalizedCode = code.Trim().ToUpper();
            return _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(course => course.Code == normalizedCode);
        }

        public async Task UpdateAsync(Course course)
        {
            _context.Courses.Update(course);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Course course)
        {
            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAggregateAsync(Course course)
        {
            var normalizedCode = course.Code.Trim().ToUpperInvariant();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var attemptIds = _context.QuizAttempts
                .Where(attempt => attempt.CourseCode == normalizedCode)
                .Select(attempt => attempt.Id);
            await _context.QuizAttemptAnswers
                .Where(answer => attemptIds.Contains(answer.AttemptId))
                .ExecuteDeleteAsync();
            await _context.QuizAttempts
                .Where(attempt => attempt.CourseCode == normalizedCode)
                .ExecuteDeleteAsync();
            await _context.Quizzes
                .Where(quiz => quiz.CourseCode == normalizedCode)
                .ExecuteDeleteAsync();
            await _context.QuestionBanks
                .Where(question => question.CourseCode == normalizedCode)
                .ExecuteDeleteAsync();
            await _context.ChatTrackerLogs
                .Where(log => log.CourseCode == normalizedCode)
                .ExecuteDeleteAsync();
            await _context.ChatSessions
                .Where(session => session.CourseCode == normalizedCode)
                .ExecuteDeleteAsync();

            var threadIds = _context.ChatThreads
                .Where(thread => thread.CourseCode == normalizedCode)
                .Select(thread => thread.Id);
            await _context.ChatMessages
                .Where(message => threadIds.Contains(message.ThreadId))
                .ExecuteDeleteAsync();
            await _context.ChatThreads
                .Where(thread => thread.CourseCode == normalizedCode)
                .ExecuteDeleteAsync();
            await _context.KnowledgeDocuments
                .Where(document => document.CourseCode == normalizedCode)
                .ExecuteDeleteAsync();

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<IEnumerable<Course>> GetBySubjectLeaderIdAsync(System.Guid userId)
        {
            return await _context.Courses
                .Where(c => c.SubjectLeaderId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}
