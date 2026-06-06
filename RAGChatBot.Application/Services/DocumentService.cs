using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IKnowledgeDocumentRepository _documentRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IUserRepository _userRepository;

        public DocumentService(
            IFileStorageService fileStorageService,
            IKnowledgeDocumentRepository documentRepository,
            ICourseRepository courseRepository,
            IUserRepository userRepository)
        {
            _fileStorageService = fileStorageService;
            _documentRepository = documentRepository;
            _courseRepository = courseRepository;
            _userRepository = userRepository;
        }

        public async Task<DocumentDto> UploadDocumentAsync(
            Stream fileStream,
            string fileName,
            long fileSize,
            string courseCode,
            string chapter,
            Guid userId,
            string userSubscriptionTier)
        {
            // 1. Validate file extension
            var extension = Path.GetExtension(fileName).ToLower();
            if (extension != ".pdf" && extension != ".docx")
            {
                throw new ArgumentException("Chỉ chấp nhận tệp tin định dạng .pdf hoặc .docx!");
            }

            // 2. Validate file size based on subscription tier
            long maxBytes = userSubscriptionTier.Equals("Premium", StringComparison.OrdinalIgnoreCase)
                ? 50 * 1024 * 1024  // 50 MB
                : 5 * 1024 * 1024;   // 5 MB

            if (fileSize > maxBytes)
            {
                string tierName = userSubscriptionTier.Equals("Premium", StringComparison.OrdinalIgnoreCase) ? "Premium" : "Free";
                throw new InvalidOperationException($"Kích thước tệp ({fileSize / 1024.0 / 1024.0:F2} MB) vượt quá giới hạn cho phép ({maxBytes / 1024.0 / 1024.0} MB) của gói tài khoản {tierName}!");
            }

            // 3. Save physical file in parallel / synchronously
            var relativePath = await _fileStorageService.SaveFileAsync(fileStream, fileName);

            // 4. Check if the uploader is the Subject Leader or Admin -> auto approve
            var user = await _userRepository.GetByIdAsync(userId);
            var isApproved = false;
            
            var courses = await _courseRepository.GetAllAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(courseCode, StringComparison.OrdinalIgnoreCase));
            if (course != null)
            {
                if (userId == course.SubjectLeaderId || (user != null && user.Role == "Admin"))
                {
                    isApproved = true;
                }
            }

            // 5. Save metadata to DB using repository
            var document = new KnowledgeDocument
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                StoragePath = relativePath,
                CourseCode = courseCode,
                Chapter = chapter,
                FileSize = fileSize,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = userId,
                IsProcessed = false,
                IsApproved = isApproved
            };

            await _documentRepository.AddAsync(document);
            await _documentRepository.SaveChangesAsync();

            var uploaderName = user?.Username ?? "N/A";
            var dto = MapToDto(document);
            dto.UploaderName = uploaderName;
            return dto;
        }

        public async Task<IEnumerable<DocumentDto>> GetDocumentsByCourseAsync(string courseCode)
        {
            var docs = await _documentRepository.GetByCourseCodeAsync(courseCode);
            var users = await _userRepository.GetAllAsync();
            var userMap = users.ToDictionary(u => u.Id, u => u.Username);

            return docs.Select(doc => new DocumentDto
            {
                Id = doc.Id,
                FileName = doc.FileName,
                StoragePath = doc.StoragePath,
                CourseCode = doc.CourseCode,
                Chapter = doc.Chapter,
                FileSize = doc.FileSize,
                UploadedAt = doc.UploadedAt,
                UploadedBy = doc.UploadedBy,
                IsProcessed = doc.IsProcessed,
                IsApproved = doc.IsApproved,
                UploaderName = userMap.TryGetValue(doc.UploadedBy, out var name) ? name : "N/A"
            }).OrderByDescending(d => d.UploadedAt);
        }

        public async Task DeleteDocumentAsync(Guid id, Guid userId, string userRole)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài liệu yêu cầu xóa!");
            }

            // Bảo mật nghiệp vụ: Chỉ cho phép người tải lên tệp hoặc Admin được quyền xóa tệp
            if (document.UploadedBy != userId && userRole != "Admin")
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa tài liệu của giảng viên khác!");
            }

            // 1. Xóa file vật lý trên dịch vụ lưu trữ (đám mây hoặc local)
            try
            {
                await _fileStorageService.DeleteFileAsync(document.StoragePath);
            }
            catch (Exception)
            {
                // Vẫn tiếp tục xóa bản ghi DB ngay cả khi file vật lý trên cloud đã bị xóa trước đó
            }

            // 2. Xóa bản ghi siêu dữ liệu trong DB
            await _documentRepository.DeleteAsync(document);
            await _documentRepository.SaveChangesAsync();
        }

        public async Task ApproveDocumentAsync(Guid id, Guid userId, string userRole)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài liệu cần phê duyệt!");
            }

            var courses = await _courseRepository.GetAllAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(document.CourseCode, StringComparison.OrdinalIgnoreCase));
            if (course == null)
            {
                throw new KeyNotFoundException("Không tìm thấy môn học liên quan đến tài liệu!");
            }

            // Kiểm tra quyền duyệt: Chỉ Trưởng bộ môn của môn đó hoặc Admin mới được duyệt
            if (course.SubjectLeaderId != userId && userRole != "Admin")
            {
                throw new UnauthorizedAccessException("Bạn không có quyền phê duyệt tài liệu cho môn học này!");
            }

            document.IsApproved = true;
            await _documentRepository.SaveChangesAsync();
        }

        public async Task<IEnumerable<ChunkDto>> GetDocumentChunksAsync(Guid documentId)
        {
            var document = await _documentRepository.GetByIdWithChunksAsync(documentId);
            if (document == null)
            {
                return Enumerable.Empty<ChunkDto>();
            }

            return document.Chunks
                .OrderBy(c => c.ChunkIndex)
                .Select(c => new ChunkDto
                {
                    ChunkIndex = c.ChunkIndex,
                    Content = c.Content,
                    HasEmbedding = c.Embedding != null
                });
        }

        public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id)
        {
            var doc = await _documentRepository.GetByIdAsync(id);
            if (doc == null) return null;

            var users = await _userRepository.GetAllAsync();
            var userMap = users.ToDictionary(u => u.Id, u => u.Username);

            var dto = MapToDto(doc);
            dto.UploaderName = userMap.TryGetValue(doc.UploadedBy, out var name) ? name : "N/A";
            return dto;
        }

        private static DocumentDto MapToDto(KnowledgeDocument doc)
        {
            return new DocumentDto
            {
                Id = doc.Id,
                FileName = doc.FileName,
                StoragePath = doc.StoragePath,
                CourseCode = doc.CourseCode,
                Chapter = doc.Chapter,
                FileSize = doc.FileSize,
                UploadedAt = doc.UploadedAt,
                UploadedBy = doc.UploadedBy,
                IsProcessed = doc.IsProcessed,
                IsApproved = doc.IsApproved
            };
        }
    }
}
