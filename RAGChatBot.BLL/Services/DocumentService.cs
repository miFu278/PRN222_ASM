using RAGChatBot.Domain.Interfaces;
using RAGChatBot.BLL.Services;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Enums;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IKnowledgeDocumentRepository _documentRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDocumentEventService _eventService;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            IFileStorageService fileStorageService,
            IKnowledgeDocumentRepository documentRepository,
            ICourseRepository courseRepository,
            IUserRepository userRepository,
            IDocumentEventService eventService,
            ILogger<DocumentService> logger)
        {
            _fileStorageService = fileStorageService;
            _documentRepository = documentRepository;
            _courseRepository = courseRepository;
            _userRepository = userRepository;
            _eventService = eventService;
            _logger = logger;
        }

        public async Task<DocumentDto> UploadDocumentAsync(
            Stream fileStream,
            string fileName,
            long fileSize,
            string courseCode,
            string chapter,
            Guid userId,
            string chunkingStrategy = "Character",
            int chunkSize = 500,
            int overlap = 50)
        {
            fileName = Path.GetFileName(fileName);
            courseCode = courseCode.Trim().ToUpperInvariant();
            chapter = chapter.Trim();
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".docx")
            {
                throw new ArgumentException("Chỉ chấp nhận tệp tin định dạng .pdf hoặc .docx!");
            }

            if (fileSize <= 0) throw new ArgumentException("Tệp tải lên đang rỗng.", nameof(fileSize));
            if (string.IsNullOrWhiteSpace(chapter)) throw new ArgumentException("Chương không được để trống.", nameof(chapter));
            if (chunkSize is < 100 or > 5000)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Kích thước chunk phải từ 100 đến 5.000.");
            if (overlap < 0 || overlap >= chunkSize)
                throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap phải từ 0 và nhỏ hơn kích thước chunk.");
            if (chunkingStrategy is not ("Character" or "Word" or "Paragraph"))
                throw new ArgumentException("Chiến lược chunk không hợp lệ.", nameof(chunkingStrategy));

            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new UnauthorizedAccessException("Không tìm thấy người dùng tải tài liệu!");
            var courses = await _courseRepository.GetAllAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(courseCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Không tìm thấy môn học!");
            var isSubjectLeader =
                string.Equals(user.Role?.Name, RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase) &&
                course.SubjectLeaderId == userId;
            if (!isSubjectLeader)
            {
                throw new UnauthorizedAccessException("Chỉ giảng viên phụ trách môn học mới được tải tài liệu lên!");
            }

            // Never trust a subscription tier supplied by the browser.
            var hasPremium = string.Equals(user.SubscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase)
                && (!user.SubscriptionExpiresAt.HasValue || user.SubscriptionExpiresAt.Value > DateTime.UtcNow);
            long maxBytes = hasPremium
                ? 50 * 1024 * 1024  // 50 MB
                : 5 * 1024 * 1024;   // 5 MB

            if (fileSize > maxBytes)
            {
                string tierName = hasPremium ? "Premium" : "Free";
                throw new InvalidOperationException($"Kích thước tệp ({fileSize / 1024.0 / 1024.0:F2} MB) vượt quá giới hạn cho phép ({maxBytes / 1024.0 / 1024.0} MB) của gói tài khoản {tierName}!");
            }

            // 3. Save physical file in parallel / synchronously
            var relativePath = await _fileStorageService.SaveFileAsync(fileStream, fileName);

            var uploaderName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : (user?.Username ?? "N/A");

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
                UploaderName = uploaderName,
                Status = DocumentStatus.Pending,
                IsApproved = false,
                ChunkingStrategy = chunkingStrategy,
                ChunkSize = chunkSize,
                Overlap = overlap
            };

            try
            {
                await _documentRepository.AddAsync(document);
                await _documentRepository.SaveChangesAsync();
            }
            catch
            {
                try
                {
                    await _fileStorageService.DeleteFileAsync(relativePath);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(cleanupException, "Không thể dọn tệp upload bị orphan: {StoragePath}", relativePath);
                }
                throw;
            }

            // Trigger SignalR event for real-time UI updates
            await _eventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
            {
                Type = "DocumentCreated",
                CourseCode = courseCode,
                EntityId = document.Id,
                Status = document.Status.ToString()
            });

            var dto = MapToDto(document);
            dto.UploaderName = uploaderName;
            return dto;
        }

        public async Task<IEnumerable<DocumentDto>> GetDocumentsByCourseAsync(string courseCode)
        {
            var docs = await _documentRepository.GetByCourseCodeAsync(courseCode);
            var users = await _userRepository.GetAllAsync();
            var userMap = users.ToDictionary(u => u.Id, u => !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.Username);

            return docs.Select(doc => new DocumentDto
            {
                Id = doc.Id,
                FileName = doc.FileName,
                StoragePath = string.Empty,
                CourseCode = doc.CourseCode,
                Chapter = doc.Chapter,
                FileSize = doc.FileSize,
                UploadedAt = doc.UploadedAt,
                UploadedBy = doc.UploadedBy,
                Status = doc.Status,
                IsApproved = doc.IsApproved,
                UploaderName = !string.IsNullOrWhiteSpace(doc.UploaderName) 
                    ? doc.UploaderName 
                    : (userMap.TryGetValue(doc.UploadedBy, out var name) ? name : "N/A"),
                ChunkingStrategy = doc.ChunkingStrategy,
                ChunkSize = doc.ChunkSize,
                Overlap = doc.Overlap
            }).OrderByDescending(d => d.UploadedAt);
        }

        public async Task DeleteDocumentAsync(Guid id, Guid userId)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài liệu yêu cầu xóa!");
            }

            var courses = await _courseRepository.GetAllAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(document.CourseCode, StringComparison.OrdinalIgnoreCase));
            var user = await _userRepository.GetByIdAsync(userId);
            var isSubjectLeader =
                string.Equals(user?.Role?.Name, RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase) &&
                course?.SubjectLeaderId == userId;
            if (!isSubjectLeader)
            {
                throw new UnauthorizedAccessException("Chỉ giảng viên phụ trách môn học mới được xóa tài liệu!");
            }

            // Delete database metadata first so users never see a record whose file is gone.
            await _documentRepository.DeleteAsync(document);
            await _documentRepository.SaveChangesAsync();

            try
            {
                await _fileStorageService.DeleteFileAsync(document.StoragePath);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Không thể dọn object storage {StoragePath}", document.StoragePath);
            }
            
            // Trigger SignalR event
            await _eventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
            {
                Type = "DocumentDeleted",
                CourseCode = document.CourseCode,
                EntityId = document.Id
            });
        }

        public async Task ApproveDocumentAsync(Guid id, Guid userId)
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

            var user = await _userRepository.GetByIdAsync(userId);
            var isSubjectLeader =
                string.Equals(user?.Role?.Name, RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase) &&
                course.SubjectLeaderId == userId;
            if (!isSubjectLeader)
            {
                throw new UnauthorizedAccessException("Chỉ giảng viên phụ trách môn học mới được phê duyệt tài liệu!");
            }

            document.IsApproved = true;
            await _documentRepository.SaveChangesAsync();

            // Trigger SignalR event
            await _eventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
            {
                Type = "DocumentApproved",
                CourseCode = document.CourseCode,
                EntityId = document.Id,
                Status = document.Status.ToString()
            });
        }

        public async Task RetryDocumentAsync(Guid id, Guid userId)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài liệu cần thử lại!");
            }

            var courses = await _courseRepository.GetAllAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(document.CourseCode, StringComparison.OrdinalIgnoreCase));
            if (course == null)
            {
                throw new KeyNotFoundException("Không tìm thấy môn học liên quan đến tài liệu!");
            }

            var user = await _userRepository.GetByIdAsync(userId);
            var isSubjectLeader =
                string.Equals(user?.Role?.Name, RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase) &&
                course.SubjectLeaderId == userId;

            if (!isSubjectLeader)
            {
                throw new UnauthorizedAccessException("Chỉ giảng viên phụ trách môn học mới được xử lý lại tài liệu!");
            }

            if (document.Status != DocumentStatus.Failed && document.Status != DocumentStatus.Success)
            {
                throw new InvalidOperationException("Chỉ có thể xử lý lại tài liệu đã hoàn thành hoặc bị lỗi!");
            }

            document.Status = DocumentStatus.Pending;
            await _documentRepository.SaveChangesAsync();

            // Kích hoạt Worker bằng cách thay đổi trạng thái và báo SignalR
            await _eventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
            {
                Type = "DocumentRetryRequested",
                CourseCode = document.CourseCode,
                EntityId = document.Id,
                Status = document.Status.ToString()
            });
        }

        public async Task<int> ReindexCourseDocumentsAsync(string courseCode, Guid userId)
        {
            var course = await _courseRepository.GetByCodeAsync(courseCode)
                ?? throw new KeyNotFoundException("Không tìm thấy môn học!");
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new UnauthorizedAccessException("Không tìm thấy người dùng!");
            var isSubjectLeader =
                string.Equals(user.Role?.Name, RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase) &&
                course.SubjectLeaderId == userId;
            if (!isSubjectLeader)
            {
                throw new UnauthorizedAccessException("Chỉ giảng viên phụ trách môn học mới được re-index tài liệu!");
            }

            var candidates = (await _documentRepository.GetByCourseCodeAsync(course.Code))
                .Where(document => document.IsApproved &&
                    (document.Status == DocumentStatus.Success || document.Status == DocumentStatus.Failed))
                .ToList();

            foreach (var candidate in candidates)
            {
                var trackedDocument = await _documentRepository.GetByIdAsync(candidate.Id);
                if (trackedDocument is not null)
                {
                    trackedDocument.Status = DocumentStatus.Pending;
                }
            }

            await _documentRepository.SaveChangesAsync();

            foreach (var candidate in candidates)
            {
                await _eventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
                {
                    Type = "DocumentReindexRequested",
                    CourseCode = candidate.CourseCode,
                    EntityId = candidate.Id,
                    Status = DocumentStatus.Pending.ToString()
                });
            }

            return candidates.Count;
        }

        public async Task UpdateDocumentMetadataAsync(Guid id, string newFileName, string newChapter, Guid userId)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài liệu để cập nhật!");
            }

            var courses = await _courseRepository.GetAllAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(document.CourseCode, StringComparison.OrdinalIgnoreCase));
            
            var user = await _userRepository.GetByIdAsync(userId);
            var isSubjectLeader =
                string.Equals(user?.Role?.Name, RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase) &&
                course?.SubjectLeaderId == userId;

            if (!isSubjectLeader)
            {
                throw new UnauthorizedAccessException("Chỉ giảng viên phụ trách môn học mới được sửa tài liệu!");
            }

            if (!string.IsNullOrWhiteSpace(newFileName))
            {
                // Ensure extension matches the original to avoid confusion
                var oldExtension = Path.GetExtension(document.FileName);
                var newExtension = Path.GetExtension(newFileName);
                if (!string.Equals(oldExtension, newExtension, StringComparison.OrdinalIgnoreCase))
                {
                    newFileName += oldExtension;
                }
                document.FileName = newFileName;
            }

            if (!string.IsNullOrWhiteSpace(newChapter))
            {
                document.Chapter = newChapter;
            }

            await _documentRepository.SaveChangesAsync();

            // Trigger SignalR event
            await _eventService.NotifyDocumentChangedAsync(new RealtimeChangeEvent
            {
                Type = "DocumentMetadataChanged",
                CourseCode = document.CourseCode,
                EntityId = document.Id,
                Status = document.Status.ToString()
            });
        }

        public async Task<IEnumerable<ChunkDto>> GetDocumentChunksAsync(Guid documentId, Guid userId)
        {
            var document = await _documentRepository.GetByIdWithChunksAsync(documentId);
            if (document == null)
            {
                return Enumerable.Empty<ChunkDto>();
            }

            await EnsureCanManageCourseAsync(document.CourseCode, userId);

            return document.Chunks
                .OrderBy(c => c.ChunkIndex)
                .Select(c => new ChunkDto
                {
                    ChunkIndex = c.ChunkIndex,
                    Content = c.Content,
                    HasEmbedding = c.Embedding != null
                });
        }

        public async Task<(Stream Content, string FileName)> DownloadDocumentAsync(Guid documentId, Guid userId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId)
                ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new UnauthorizedAccessException("Phiên người dùng không hợp lệ.");

            var isSubjectLeader = await CanManageCourseAsync(document.CourseCode, user);
            var isStudent = string.Equals(
                user.Role?.Name,
                RoleNames.Student,
                StringComparison.OrdinalIgnoreCase);
            if (!isSubjectLeader &&
                (!isStudent || !document.IsApproved || document.Status != DocumentStatus.Success))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền tải tài liệu này.");
            }

            return (await _fileStorageService.OpenReadAsync(document.StoragePath), document.FileName);
        }

        public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id)
        {
            var doc = await _documentRepository.GetByIdAsync(id);
            if (doc == null) return null;

            var users = await _userRepository.GetAllAsync();
            var userMap = users.ToDictionary(u => u.Id, u => !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : u.Username);

            var dto = MapToDto(doc);
            dto.UploaderName = !string.IsNullOrWhiteSpace(doc.UploaderName) 
                ? doc.UploaderName 
                : (userMap.TryGetValue(doc.UploadedBy, out var name) ? name : "N/A");
            return dto;
        }

        private static DocumentDto MapToDto(KnowledgeDocument doc)
        {
            return new DocumentDto
            {
                Id = doc.Id,
                FileName = doc.FileName,
                StoragePath = string.Empty,
                CourseCode = doc.CourseCode,
                Chapter = doc.Chapter,
                FileSize = doc.FileSize,
                UploadedAt = doc.UploadedAt,
                UploadedBy = doc.UploadedBy,
                Status = doc.Status,
                IsApproved = doc.IsApproved,
                ChunkingStrategy = doc.ChunkingStrategy,
                ChunkSize = doc.ChunkSize,
                Overlap = doc.Overlap
            };
        }

        private async Task EnsureCanManageCourseAsync(string courseCode, Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new UnauthorizedAccessException("Phiên người dùng không hợp lệ.");
            if (!await CanManageCourseAsync(courseCode, user))
            {
                throw new UnauthorizedAccessException("Bạn không quản lý môn học của tài liệu này.");
            }
        }

        private async Task<bool> CanManageCourseAsync(string courseCode, User user)
        {
            if (!string.Equals(user.Role?.Name, RoleNames.Lecturer, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var course = await _courseRepository.GetByCodeAsync(courseCode);
            return course?.SubjectLeaderId == user.Id;
        }
    }
}
