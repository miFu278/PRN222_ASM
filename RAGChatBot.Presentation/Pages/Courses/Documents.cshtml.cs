using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.BLL.DTOs;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Courses
{
    [Authorize]
    public class DocumentsModel : PageModel
    {
        private readonly IDocumentService _documentService;
        private readonly ICourseService _courseService;

        public DocumentsModel(IDocumentService documentService, ICourseService courseService)
        {
            _documentService = documentService;
            _courseService = courseService;
        }

        [BindProperty(SupportsGet = true)]
        public string CourseCode { get; set; } = string.Empty;

        public string CourseName { get; set; } = string.Empty;
        public string? CourseDescription { get; set; }
        public string? SubjectLeaderName { get; set; }

        public IEnumerable<DocumentDto> Documents { get; set; } = new List<DocumentDto>();

        public bool CanUpload { get; set; }
        public bool CanApprove { get; set; }
        public bool CanDelete { get; set; }
        public string CurrentSubscriptionTier { get; set; } = "Free";
        public Guid CurrentUserId { get; set; }

        [BindProperty]
        public string Chapter { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? UploadedFile { get; set; }

        [BindProperty]
        public string ChunkingStrategy { get; set; } = "Character";

        [BindProperty]
        public int ChunkSize { get; set; } = 500;

        [BindProperty]
        public int Overlap { get; set; } = 50;

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadCourseDetails();
            await LoadDocuments();
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            await LoadCourseDetails();
            if (!CanUpload) return Forbid();
            
            if (UploadedFile == null || string.IsNullOrWhiteSpace(Chapter))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng chọn file và nhập chương.");
                await LoadDocuments();
                return Page();
            }

            try
            {
                long maxBytes = CurrentSubscriptionTier == "Premium" ? 50 * 1024 * 1024 : 5 * 1024 * 1024;
                if (UploadedFile.Length > maxBytes)
                {
                    ModelState.AddModelError(string.Empty, $"File quá lớn. Giới hạn là {(maxBytes / 1024 / 1024)}MB.");
                    await LoadDocuments();
                    return Page();
                }

                using var memoryStream = new MemoryStream();
                await UploadedFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                await _documentService.UploadDocumentAsync(
                    memoryStream,
                    UploadedFile.FileName,
                    UploadedFile.Length,
                    CourseCode,
                    Chapter,
                    CurrentUserId,
                    CurrentSubscriptionTier,
                    ChunkingStrategy,
                    ChunkSize,
                    Overlap
                );

                TempData["SuccessMessage"] = "Đã tải lên tài liệu thành công.";
                return RedirectToPage(new { CourseCode });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi: {ex.Message}");
                await LoadDocuments();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostApproveAsync(Guid documentId)
        {
            await LoadCourseDetails();
            if (!CanApprove) return Forbid();
            try
            {
                await _documentService.ApproveDocumentAsync(documentId, CurrentUserId);
                TempData["SuccessMessage"] = "Đã phê duyệt tài liệu.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }
            return RedirectToPage(new { CourseCode });
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid documentId)
        {
            await LoadCourseDetails();
            if (!CanDelete) return Forbid();
            try
            {
                await _documentService.DeleteDocumentAsync(documentId, CurrentUserId);
                TempData["SuccessMessage"] = "Đã xóa tài liệu.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }
            return RedirectToPage(new { CourseCode });
        }

        public async Task<IActionResult> OnPostRetryAsync(Guid documentId)
        {
            await LoadCourseDetails();
            try
            {
                await _documentService.RetryDocumentAsync(documentId, CurrentUserId);
                TempData["SuccessMessage"] = "Đã yêu cầu thử lại tài liệu.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }
            return RedirectToPage(new { CourseCode });
        }

        public async Task<IActionResult> OnPostReindexCourseAsync()
        {
            await LoadCourseDetails();
            if (!CanApprove) return Forbid();

            try
            {
                var count = await _documentService.ReindexCourseDocumentsAsync(CourseCode, CurrentUserId);
                TempData["SuccessMessage"] = count == 0
                    ? "Không có tài liệu nào cần re-index."
                    : $"Đã đưa {count} tài liệu vào hàng đợi re-index.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi re-index: {ex.Message}";
            }

            return RedirectToPage(new { CourseCode });
        }

        public async Task<IActionResult> OnGetStatusAsync()
        {
            var documents = await _documentService.GetDocumentsByCourseAsync(CourseCode);
            if (User.IsInRole(RoleNames.Student))
            {
                documents = documents.Where(d => d.IsApproved && d.Status == Domain.Enums.DocumentStatus.Success);
            }
            var statusList = documents.Select(d => new
            {
                id = d.Id,
                status = d.Status.ToString().ToLower(),
                isApproved = d.IsApproved
            });
            return new JsonResult(statusList);
        }

        public async Task<IActionResult> OnGetChunksAsync(Guid id)
        {
            if (User.IsInRole(RoleNames.Student))
            {
                return Forbid();
            }

            var chunks = await _documentService.GetDocumentChunksAsync(id);
            return new JsonResult(chunks);
        }

        private async Task LoadCourseDetails()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userIdStr, out var userId);
            CurrentUserId = userId;
            CurrentSubscriptionTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";

            var isLecturer = User.IsInRole(RoleNames.Lecturer);
            var isAdmin = User.IsInRole(RoleNames.Admin);
            CanUpload = false;

            var courses = await _courseService.GetAllCoursesAsync();
            var course = courses.FirstOrDefault(c => c.Code.Equals(CourseCode, StringComparison.OrdinalIgnoreCase));
            
            if (course != null)
            {
                CourseName = course.Name;
                CourseDescription = course.Description;
                SubjectLeaderName = course.SubjectLeaderName;

                var isLeader = course.SubjectLeaderId == CurrentUserId;
                CanUpload = isLeader || isAdmin;
                CanApprove = isLeader || isAdmin;
                CanDelete = isLeader || isAdmin;
            }
        }

        private async Task LoadDocuments()
        {
            Documents = await _documentService.GetDocumentsByCourseAsync(CourseCode);
            if (User.IsInRole(RoleNames.Student))
            {
                Documents = Documents.Where(d => d.IsApproved && d.Status == Domain.Enums.DocumentStatus.Success);
            }
        }
    }
}
