using RAGChatBot.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IKnowledgeDocumentRepository
    {
        Task<KnowledgeDocument?> GetByIdAsync(System.Guid id);
        Task<IEnumerable<KnowledgeDocument>> GetByCourseCodeAsync(string courseCode);
        Task AddAsync(KnowledgeDocument document);
        Task DeleteAsync(KnowledgeDocument document);
        Task SaveChangesAsync();
    }
}
