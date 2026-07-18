using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RAGChatBot.BLL.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages
{
    [Authorize]
    [EnableRateLimiting("StudentChatLimit")]
    public class ChatApiModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatApiModel> _logger;

        public ChatApiModel(IChatService chatService, ILogger<ChatApiModel> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest? request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return new JsonResult(new { reply = "Vui lòng nhập câu hỏi." });
            }

            if (request.Message.Length > 4000)
            {
                return new JsonResult(new { reply = "Câu hỏi quá dài. Vui lòng giới hạn trong 4.000 ký tự." })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return new JsonResult(new { reply = "Phiên đăng nhập không hợp lệ." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            try
            {
                var result = await _chatService.SendMessageAsync(
                    userId,
                    request.Message,
                    request.CourseCode,
                    request.ThreadId);

                if (result is null)
                {
                    return new JsonResult(new { reply = "Không tìm thấy luồng chat." })
                    {
                        StatusCode = StatusCodes.Status404NotFound
                    };
                }

                return new JsonResult(new
                {
                    reply = result.Reply,
                    remaining = result.Remaining,
                    threadId = result.ThreadId,
                    outOfCredits = result.OutOfCredits,
                    isError = result.IsError,
                    sources = result.Sources.Select(source => new
                    {
                        documentId = source.DocumentId,
                        fileName = source.FileName,
                        courseCode = source.CourseCode,
                        chunkIndex = source.ChunkIndex,
                        relevance = source.Relevance,
                        content = source.Content
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xử lý yêu cầu chat: {Message}", request.Message);
                return new JsonResult(new
                {
                    reply = "Xin lỗi, hệ thống đang gặp sự cố. Vui lòng thử lại sau."
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
            public string? CourseCode { get; set; }
            public Guid? ThreadId { get; set; }
        }
    }
}
