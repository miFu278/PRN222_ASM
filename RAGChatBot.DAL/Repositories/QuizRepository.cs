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

        public Task<QuizAttempt?> GetAttemptWithAnswersAsync(Guid attemptId)
            => _db.QuizAttempts
                .Include(attempt => attempt.Answers.OrderBy(answer => answer.DisplayOrder))
                .FirstOrDefaultAsync(attempt => attempt.Id == attemptId);

        public Task<QuizAttempt?> GetInProgressAttemptAsync(Guid userId, Guid quizId)
            => _db.QuizAttempts
                .Include(attempt => attempt.Answers.OrderBy(answer => answer.DisplayOrder))
                .FirstOrDefaultAsync(attempt => attempt.UserId == userId &&
                    attempt.QuizId == quizId && attempt.Status == QuizAttemptStatus.InProgress);

        public Task<int> GetAttemptCountAsync(Guid userId, Guid quizId)
            => _db.QuizAttempts.CountAsync(attempt => attempt.UserId == userId && attempt.QuizId == quizId);

        public async Task<IReadOnlyList<QuizAttempt>> GetAttemptsByCourseAsync(string courseCode)
        {
            var normalizedCourseCode = courseCode.Trim().ToLower();
            return await _db.QuizAttempts
                .AsNoTracking()
                .Where(attempt => attempt.CourseCode.ToLower() == normalizedCourseCode)
                .OrderByDescending(attempt => attempt.AttemptedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<QuizAttempt>> GetAttemptsByUserAsync(Guid userId, string? courseCode = null)
        {
            var query = _db.QuizAttempts.AsNoTracking().Where(attempt => attempt.UserId == userId);
            if (!string.IsNullOrWhiteSpace(courseCode))
            {
                var normalized = courseCode.Trim().ToLower();
                query = query.Where(attempt => attempt.CourseCode.ToLower() == normalized);
            }

            return await query.OrderByDescending(attempt => attempt.StartedAt).ToListAsync();
        }

        public async Task AddQuestionAsync(QuestionBank question)
        {
            _db.QuestionBanks.Add(question);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateQuestionAsync(QuestionBank question)
        {
            _db.QuestionBanks.Update(question);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteQuestionAsync(Guid id)
        {
            var question = await _db.QuestionBanks.FindAsync(id);
            if (question != null)
            {
                _db.QuestionBanks.Remove(question);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<QuestionBank?> GetQuestionByIdAsync(Guid id)
        {
            return await _db.QuestionBanks
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<KnowledgeDocument?> GetFirstDocumentByCourseAsync(string courseCode)
        {
            var normalized = courseCode.Trim().ToLower();
            return await _db.KnowledgeDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.CourseCode.ToLower() == normalized);
        }

        public async Task<IReadOnlyList<Quiz>> GetQuizzesByCourseAsync(string courseCode)
        {
            var normalized = courseCode.Trim().ToLower();
            return await _db.Quizzes
                .AsNoTracking()
                .Where(q => q.CourseCode.ToLower() == normalized)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task AddQuizAsync(Quiz quiz)
        {
            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteQuizAsync(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz != null)
            {
                quiz.IsPublished = false;
                await _db.SaveChangesAsync();
            }
        }

        public async Task<Quiz?> GetQuizByIdAsync(Guid id)
        {
            return await _db.Quizzes
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public Task SaveChangesAsync() => _db.SaveChangesAsync();
    }
}
