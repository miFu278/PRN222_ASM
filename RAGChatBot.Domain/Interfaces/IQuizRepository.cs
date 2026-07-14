using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IQuizRepository
    {
        Task<KnowledgeDocument?> GetDocumentAsync(Guid documentId);
        Task<IReadOnlyList<string>> GetDocumentChunkContentsAsync(Guid documentId);
        Task ReplaceQuestionsAsync(Guid documentId, IReadOnlyList<QuestionBank> questions);
        Task<IReadOnlyList<QuestionBank>> GetQuestionsByCourseAsync(string courseCode);
        Task<IReadOnlyDictionary<Guid, QuestionBank>> GetQuestionsByIdsAsync(IReadOnlyCollection<Guid> questionIds);
        Task<QuizAttempt> AddAttemptAsync(QuizAttempt attempt);
        Task<QuizAttempt?> GetAttemptWithAnswersAsync(Guid attemptId);
        Task<QuizAttempt?> GetInProgressAttemptAsync(Guid userId, Guid quizId);
        Task<int> GetAttemptCountAsync(Guid userId, Guid quizId);
        Task<IReadOnlyList<QuizAttempt>> GetAttemptsByCourseAsync(string courseCode);
        Task<IReadOnlyList<QuizAttempt>> GetAttemptsByUserAsync(Guid userId, string? courseCode = null);
        Task AddQuestionAsync(QuestionBank question);
        Task UpdateQuestionAsync(QuestionBank question);
        Task DeleteQuestionAsync(Guid id);
        Task<QuestionBank?> GetQuestionByIdAsync(Guid id);
        Task<KnowledgeDocument?> GetFirstDocumentByCourseAsync(string courseCode);
        Task<IReadOnlyList<Quiz>> GetQuizzesByCourseAsync(string courseCode);
        Task AddQuizAsync(Quiz quiz);
        Task DeleteQuizAsync(Guid id);
        Task<Quiz?> GetQuizByIdAsync(Guid id);
        Task SaveChangesAsync();
    }
}
