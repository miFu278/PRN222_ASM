using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Enums;
using RAGChatBot.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.DAL.Repositories
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
        public async Task<IReadOnlyList<RelevantDocumentChunk>> SearchSimilarChunksAsync(
            string courseCode,
            float[] queryEmbedding,
            int topK = 8,
            double maxDistance = 0.55)
        {
            var pgVector = new Pgvector.Vector(queryEmbedding);
            var normalizedCourseCode = courseCode.Trim().ToUpper();
            return await _context.DocumentChunks
                .AsNoTracking()
                .Where(c => c.Embedding != null &&
                            c.Document.CourseCode == normalizedCourseCode &&
                            c.Document.IsApproved &&
                            c.Document.Status == DocumentStatus.Success)
                .Select(c => new RelevantDocumentChunk
                {
                    DocumentId = c.DocumentId,
                    FileName = c.Document.FileName,
                    CourseCode = c.Document.CourseCode,
                    ChunkIndex = c.ChunkIndex,
                    Content = c.Content,
                    Distance = c.Embedding!.CosineDistance(pgVector)
                })
                .Where(match => match.Distance <= maxDistance)
                .OrderBy(match => match.Distance)
                .Take(topK)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
