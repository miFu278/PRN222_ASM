using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RAGChatBot.Infrastructure.Interfaces;
using RAGChatBot.Infrastructure.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.DataAccess.Repositories
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

        public async Task<KnowledgeDocument?> GetByIdWithChunksAsync(System.Guid id)
        {
            return await _context.KnowledgeDocuments
                .AsNoTracking()
                .Include(d => d.Chunks)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<IEnumerable<KnowledgeDocument>> GetByCourseCodeAsync(string courseCode)
        {
            return await _context.KnowledgeDocuments
                .AsNoTracking()
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
        public async Task<IEnumerable<DocumentChunk>> SearchSimilarChunksAsync(string? courseCode, float[] queryEmbedding, int topK = 5)
        {
            var pgVector = new Pgvector.Vector(queryEmbedding);
            return await _context.DocumentChunks
                .Include(c => c.Document)
                .Where(c => (string.IsNullOrEmpty(courseCode) || c.Document.CourseCode == courseCode) && c.Document.IsApproved && c.Document.Status == RAGChatBot.Infrastructure.Enums.DocumentStatus.Success)
                .OrderBy(c => c.Embedding!.CosineDistance(pgVector))
                .Take(topK)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
