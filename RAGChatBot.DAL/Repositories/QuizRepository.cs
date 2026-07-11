using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public sealed class QuizRepository : IQuizRepository
    {
        private readonly AppDbContext _db;

        public QuizRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<KnowledgeDocument?> GetDocumentAsync(Guid documentId)
            => _db.KnowledgeDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(document => document.Id == documentId);

        public async Task<IReadOnlyList<string>> GetDocumentChunkContentsAsync(Guid documentId)
            => await _db.DocumentChunks
                .AsNoTracking()
                .Where(chunk => chunk.DocumentId == documentId)
                .OrderBy(chunk => chunk.ChunkIndex)
                .Select(chunk => chunk.Content)
                .ToListAsync();

        public async Task ReplaceQuestionsAsync(
            Guid documentId,
            IReadOnlyList<QuestionBank> questions)
        {
            var existingQuestions = await _db.QuestionBanks
                .Where(question => question.DocumentId == documentId)
                .ToListAsync();

            if (existingQuestions.Count > 0)
            {
                _db.QuestionBanks.RemoveRange(existingQuestions);
            }

            _db.QuestionBanks.AddRange(questions);
            await _db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<QuestionBank>> GetQuestionsByCourseAsync(string courseCode)
        {
            var normalizedCourseCode = courseCode.Trim().ToLower();
            return await _db.QuestionBanks
                .AsNoTracking()
                .Where(question => question.CourseCode.ToLower() == normalizedCourseCode)
                .OrderBy(question => question.CreatedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyDictionary<Guid, QuestionBank>> GetQuestionsByIdsAsync(
            IReadOnlyCollection<Guid> questionIds)
            => await _db.QuestionBanks
                .AsNoTracking()
                .Where(question => questionIds.Contains(question.Id))
                .ToDictionaryAsync(question => question.Id);

        public async Task AddAttemptAsync(QuizAttempt attempt)
        {
            _db.QuizAttempts.Add(attempt);
            await _db.SaveChangesAsync();
        }
    }
}
