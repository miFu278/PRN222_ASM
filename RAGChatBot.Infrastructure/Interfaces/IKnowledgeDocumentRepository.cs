using RAGChatBot.Infrastructure.Models;

namespace RAGChatBot.Infrastructure.Interfaces
{
    public interface IKnowledgeDocumentRepository
    {
        Task<KnowledgeDocument?> GetByIdAsync(Guid id);
        Task<KnowledgeDocument?> GetByIdWithChunksAsync(Guid id);
        Task<IEnumerable<KnowledgeDocument>> GetByCourseCodeAsync(string courseCode);
        Task AddAsync(KnowledgeDocument document);
        Task DeleteAsync(KnowledgeDocument document);
        Task<IEnumerable<DocumentChunk>> SearchSimilarChunksAsync(string? courseCode, float[] queryEmbedding, int topK = 5);
        Task SaveChangesAsync();
    }
}
