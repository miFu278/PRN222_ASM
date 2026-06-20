using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using RAGChatBot.Application.Common.Interfaces;
using System.Security.Claims;

namespace RAGChatBot.WebMVC.Controllers
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

                // Kiá»ƒm tra xem user hiá»‡n táº¡i cÃ³ pháº£i lÃ  Subject Leader cá»§a mÃ´n há»c khÃ´ng
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
                _logger.LogError(ex, "Lá»—i khi táº£i danh sÃ¡ch tÃ i liá»‡u cho mÃ´n há»c {CourseCode}", courseCode);
                TempData["ErrorMessage"] = $"Lá»—i khi táº£i danh sÃ¡ch tÃ i liá»‡u: {ex.Message}";
                return View(new List<DocumentDto>());
            }
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Upload(IFormFile file, string courseCode, string chapter)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lÃ²ng chá»n má»™t tá»‡p tin há»£p lá»‡ Ä‘á»ƒ táº£i lÃªn!";
                return RedirectToAction("Index", new { courseCode });
            }

            if (string.IsNullOrEmpty(courseCode) || string.IsNullOrEmpty(chapter))
            {
                TempData["ErrorMessage"] = "Vui lÃ²ng Ä‘iá»n Ä‘áº§y Ä‘á»§ thÃ´ng tin mÃ£ mÃ´n há»c vÃ  chÆ°Æ¡ng!";
                return RedirectToAction("Index", new { courseCode });
            }

            // Láº¥y thÃ´ng tin user hiá»‡n táº¡i tá»« Claims
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var subscriptionTier = User.FindFirstValue("SubscriptionTier") ?? "Free";

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y thÃ´ng tin Ä‘á»‹nh danh ngÆ°á»i dÃ¹ng há»£p lá»‡!";
                return RedirectToAction("Index", new { courseCode });
            }

            // Kiá»ƒm tra quyá»n: Chá»‰ TrÆ°á»Ÿng bá»™ mÃ´n cá»§a mÃ´n Ä‘Ã³ má»›i Ä‘Æ°á»£c phÃ©p upload
            var isSubjectLeader = await _courseService.IsSubjectLeaderAsync(courseCode, userId);
            if (!isSubjectLeader)
            {
                TempData["ErrorMessage"] = "Chá»‰ cÃ³ TrÆ°á»Ÿng bá»™ mÃ´n cá»§a mÃ´n há»c nÃ y má»›i Ä‘Æ°á»£c phÃ©p táº£i lÃªn tÃ i liá»‡u!";
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

                TempData["SuccessMessage"] = $"Táº£i lÃªn tÃ i liá»‡u '{file.FileName}' thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i xáº£y ra khi táº£i lÃªn tÃ i liá»‡u {FileName} cho mÃ´n há»c {CourseCode}", file.FileName, courseCode);
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
                TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y thÃ´ng tin Ä‘á»‹nh danh ngÆ°á»i dÃ¹ng há»£p lá»‡!";
                return RedirectToAction("Index", new { courseCode });
            }

            try
            {
                await _documentService.DeleteDocumentAsync(id, userId);
                TempData["SuccessMessage"] = "XÃ³a tÃ i liá»‡u thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i xáº£y ra khi xÃ³a tÃ i liá»‡u {Id}", id);
                TempData["ErrorMessage"] = $"Lá»—i khi xÃ³a tÃ i liá»‡u: {ex.Message}";
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
                TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y thÃ´ng tin Ä‘á»‹nh danh ngÆ°á»i dÃ¹ng há»£p lá»‡!";
                return RedirectToAction("Index", new { courseCode });
            }

            try
            {
                await _documentService.ApproveDocumentAsync(id, userId);
                TempData["SuccessMessage"] = "ÄÃ£ phÃª duyá»‡t tÃ i liá»‡u thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi duyá»‡t tÃ i liá»‡u {DocId}", id);
                TempData["ErrorMessage"] = $"Lá»—i khi duyá»‡t tÃ i liá»‡u: {ex.Message}";
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
                _logger.LogError(ex, "Lá»—i khi láº¥y danh sÃ¡ch chunks cho tÃ i liá»‡u {DocId}", id);
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
                // 1. Kiá»ƒm tra thuáº­t toÃ¡n Chunking
                var sampleText = "Há»‡ thá»‘ng RAG ChatBot hoáº¡t Ä‘á»™ng báº±ng cÃ¡ch chia nhá» tÃ i liá»‡u há»c táº­p cá»§a giáº£ng viÃªn, chuyá»ƒn Ä‘á»•i thÃ nh vector nhÃºng vÃ  tÃ¬m kiáº¿m ngá»¯ cáº£nh tÆ°Æ¡ng Ä‘á»“ng khi há»c sinh Ä‘áº·t cÃ¢u há»i.";
                var chunkingService = HttpContext.RequestServices.GetRequiredService<IChunkingService>();
                var chunks = chunkingService.ChunkText(sampleText, 30, 5);
                testResult["ChunkingTest"] = new
                {
                    Success = true,
                    OriginalLength = sampleText.Length,
                    ChunkCount = chunks.Count,
                    Chunks = chunks
                };

                // 2. Kiá»ƒm tra káº¿t ná»‘i API Embedding (9router)
                var embeddingService = HttpContext.RequestServices.GetRequiredService<IEmbeddingService>();
                var vector = await embeddingService.GenerateEmbeddingAsync("Hello, RAG Chatbot!");
                testResult["EmbeddingApiTest"] = new
                {
                    Success = true,
                    VectorLength = vector.Length,
                    FirstThreeValues = vector.Take(3).ToArray()
                };

                testResult["OverallStatus"] = "Táº¥t cáº£ cÃ¡c káº¿t ná»‘i vÃ  thuáº­t toÃ¡n hoáº¡t Ä‘á»™ng Tá»T!";
            }
            catch (Exception ex)
            {
                testResult["OverallStatus"] = "Káº¿t ná»‘i THáº¤T Báº I!";
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

