using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using RAGChatBot.Application.Common.Interfaces;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Controllers
{
    [Authorize]
    public class DocumentController : Controller
    {
        private readonly IDocumentService _documentService;
        private readonly ICourseService _courseService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(IDocumentService documentService, ICourseService courseService, ILogger<DocumentController> logger)
        {
            _documentService = documentService;
            _courseService = courseService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string courseCode = "CS101")
        {
            ViewBag.CourseCode = courseCode;
            try
            {
                var documents = await _documentService.GetDocumentsByCourseAsync(courseCode);

                if (User.IsInRole("Student"))
                {
                    documents = documents.Where(d => d.IsApproved).ToList();
                }

                // Kiểm tra xem user hiện tại có phải là Subject Leader của môn học không
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isSubjectLeader = false;
                if (Guid.TryParse(userIdStr, out var userId))
                {
                    isSubjectLeader = await _courseService.IsSubjectLeaderAsync(courseCode, userId);
                }
                ViewBag.IsSubjectLeader = isSubjectLeader;

                return View(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải danh sách tài liệu cho môn học {CourseCode}", courseCode);
                TempData["ErrorMessage"] = $"Lỗi khi tải danh sách tài liệu: {ex.Message}";
                return View(new List<DocumentDto>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Upload(IFormFile file, string courseCode, string chapter)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một tệp tin hợp lệ để tải lên!";
                return RedirectToAction("Index", new { courseCode });
            }

            if (string.IsNullOrEmpty(courseCode) || string.IsNullOrEmpty(chapter))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ thông tin mã môn học và chương!";
                return RedirectToAction("Index", new { courseCode });
            }

            // Lấy thông tin user hiện tại từ Claims
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var subscriptionTier = User.FindFirstValue("SubscriptionTier") ?? "Free";

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin định danh người dùng hợp lệ!";
                return RedirectToAction("Index", new { courseCode });
            }

            // Kiểm tra quyền: Chỉ Trưởng bộ môn của môn đó mới được phép upload
            var isSubjectLeader = await _courseService.IsSubjectLeaderAsync(courseCode, userId);
            if (!isSubjectLeader)
            {
                TempData["ErrorMessage"] = "Chỉ có Trưởng bộ môn của môn học này mới được phép tải lên tài liệu!";
                return RedirectToAction("Index", new { courseCode });
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    await _documentService.UploadDocumentAsync(
                        stream,
                        file.FileName,
                        file.Length,
                        courseCode,
                        chapter,
                        userId,
                        subscriptionTier
                    );
                }

                TempData["SuccessMessage"] = $"Tải lên tài liệu '{file.FileName}' thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi tải lên tài liệu {FileName} cho môn học {CourseCode}", file.FileName, courseCode);
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction("Index", new { courseCode });
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Delete(Guid id, string courseCode)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin định danh người dùng hợp lệ!";
                return RedirectToAction("Index", new { courseCode });
            }

            try
            {
                await _documentService.DeleteDocumentAsync(id, userId);
                TempData["SuccessMessage"] = "Xóa tài liệu thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi xóa tài liệu {Id}", id);
                TempData["ErrorMessage"] = $"Lỗi khi xóa tài liệu: {ex.Message}";
            }

            return RedirectToAction("Index", new { courseCode });
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Approve(Guid id, string courseCode)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin định danh người dùng hợp lệ!";
                return RedirectToAction("Index", new { courseCode });
            }

            try
            {
                await _documentService.ApproveDocumentAsync(id, userId);
                TempData["SuccessMessage"] = "Đã phê duyệt tài liệu thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi duyệt tài liệu {DocId}", id);
                TempData["ErrorMessage"] = $"Lỗi khi duyệt tài liệu: {ex.Message}";
            }

            return RedirectToAction("Index", new { courseCode });
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus(string courseCode)
        {
            try
            {
                var documents = await _documentService.GetDocumentsByCourseAsync(courseCode);
                if (User.IsInRole("Student"))
                {
                    documents = documents.Where(d => d.IsApproved).ToList();
                }
                return Json(documents.Select(d => new { id = d.Id, isProcessed = d.IsProcessed }));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetChunks(Guid id)
        {
            try
            {
                if (User.IsInRole("Student"))
                {
                    var doc = await _documentService.GetDocumentByIdAsync(id);
                    if (doc == null || !doc.IsApproved)
                    {
                        return Forbid();
                    }
                }
                var chunks = await _documentService.GetDocumentChunksAsync(id);
                return Json(chunks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách chunks cho tài liệu {DocId}", id);
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> TestConnection()
        {
            var testResult = new System.Collections.Generic.Dictionary<string, object>();
            try
            {
                // 1. Kiểm tra thuật toán Chunking
                var sampleText = "Hệ thống RAG ChatBot hoạt động bằng cách chia nhỏ tài liệu học tập của giảng viên, chuyển đổi thành vector nhúng và tìm kiếm ngữ cảnh tương đồng khi học sinh đặt câu hỏi.";
                var chunkingService = HttpContext.RequestServices.GetRequiredService<IChunkingService>();
                var chunks = chunkingService.ChunkText(sampleText, 30, 5);
                testResult["ChunkingTest"] = new
                {
                    Success = true,
                    OriginalLength = sampleText.Length,
                    ChunkCount = chunks.Count,
                    Chunks = chunks
                };

                // 2. Kiểm tra kết nối API Embedding (9router)
                var embeddingService = HttpContext.RequestServices.GetRequiredService<IEmbeddingService>();
                var vector = await embeddingService.GenerateEmbeddingAsync("Hello, RAG Chatbot!");
                testResult["EmbeddingApiTest"] = new
                {
                    Success = true,
                    VectorLength = vector.Length,
                    FirstThreeValues = vector.Take(3).ToArray()
                };

                testResult["OverallStatus"] = "Tất cả các kết nối và thuật toán hoạt động TỐT!";
            }
            catch (Exception ex)
            {
                testResult["OverallStatus"] = "Kết nối THẤT BẠI!";
                testResult["ErrorMessage"] = ex.Message;
                if (ex.InnerException != null)
                {
                    testResult["InnerErrorMessage"] = ex.InnerException.Message;
                }
            }
            return Json(testResult);
        }
    }
}
