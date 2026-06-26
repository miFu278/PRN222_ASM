using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Application.Common.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace RAGChatBot.Presentation.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("ChatPolicy")]
    public class ChatApiModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatApiModel> _logger;

        public ChatApiModel(IChatService chatService, ILogger<ChatApiModel> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return new JsonResult(new { reply = "Vui lòng nhập câu hỏi." });
            }

            try
            {
                var reply = await _chatService.GetChatResponseAsync(request.Message, request.CourseCode);
                return new JsonResult(new { reply });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xử lý yêu cầu chat: {Message}", request.Message);
                return new JsonResult(new { reply = "Xin lỗi, hệ thống đang gặp sự cố. Vui lòng thử lại sau." })
                {
                    StatusCode = 200 // Trả 200 để client hiển thị lỗi thân thiện
                };
            }
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
            public string? CourseCode { get; set; }
        }
    }
}
