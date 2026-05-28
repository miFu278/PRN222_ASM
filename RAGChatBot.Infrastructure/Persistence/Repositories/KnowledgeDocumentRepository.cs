using Microsoft.EntityFrameworkCore;
using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.Persistence.Repositories
{
    public class KnowledgeDocumentRepository : IKnowledgeDocumentRepository
    {
        private readonly AppDbContext _context;

        public KnowledgeDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<KnowledgeDocument?> GetByIdAsync(System.Guid id)
        {
            return await _context.KnowledgeDocuments.FindAsync(id);
        }

        public async Task<IEnumerable<KnowledgeDocument>> GetByCourseCodeAsync(string courseCode)
        {
            return await _context.KnowledgeDocuments
                .Where(d => d.CourseCode == courseCode)
                .ToListAsync();
        }

        public async Task AddAsync(KnowledgeDocument document)
        {
            await _context.KnowledgeDocuments.AddAsync(document);
        }

        public async Task DeleteAsync(KnowledgeDocument document)
        {
            _context.KnowledgeDocuments.Remove(document);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
