using RAGChatBot.BLL.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public interface IDocumentService
    {
        Task<DocumentDto> UploadDocumentAsync(
            Stream fileStream, 
            string fileName, 
            long fileSize, 
            string courseCode, 
            string chapter, 
            Guid userId, 
            string userSubscriptionTier,
            string chunkingStrategy = "Character",
            int chunkSize = 500,
            int overlap = 50);

        Task<IEnumerable<DocumentDto>> GetDocumentsByCourseAsync(string courseCode);

        Task DeleteDocumentAsync(Guid id, Guid userId);

        Task ApproveDocumentAsync(Guid id, Guid userId);

        Task RetryDocumentAsync(Guid id, Guid userId);

        Task<IEnumerable<ChunkDto>> GetDocumentChunksAsync(Guid documentId);

        Task UpdateDocumentMetadataAsync(Guid id, string newFileName, string newChapter, Guid userId);

        Task<DocumentDto?> GetDocumentByIdAsync(Guid id);
    }
}
