using RAGChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Services
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
            string userSubscriptionTier);

        Task<IEnumerable<DocumentDto>> GetDocumentsByCourseAsync(string courseCode);

        Task DeleteDocumentAsync(Guid id, Guid userId, string userRole);

        Task ApproveDocumentAsync(Guid id, Guid userId, string userRole);

        Task<IEnumerable<ChunkDto>> GetDocumentChunksAsync(Guid documentId);

        Task<DocumentDto?> GetDocumentByIdAsync(Guid id);
    }
}
