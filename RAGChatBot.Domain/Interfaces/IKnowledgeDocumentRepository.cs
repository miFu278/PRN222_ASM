using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Models;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IKnowledgeDocumentRepository
    {
        Task<KnowledgeDocument?> GetByIdAsync(Guid id);
        Task<KnowledgeDocument?> GetByIdWithChunksAsync(Guid id);
        Task<IEnumerable<KnowledgeDocument>> GetByCourseCodeAsync(string courseCode);
        Task AddAsync(KnowledgeDocument document);
        Task DeleteAsync(KnowledgeDocument document);
        Task<IReadOnlyList<RelevantDocumentChunk>> SearchSimilarChunksAsync(
            string courseCode,
            float[] queryEmbedding,
            int topK = 8,
            double maxDistance = 0.55);
        Task SaveChangesAsync();
    }
}
