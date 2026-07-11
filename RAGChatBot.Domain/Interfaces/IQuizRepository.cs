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
        Task AddAttemptAsync(QuizAttempt attempt);
    }
}
