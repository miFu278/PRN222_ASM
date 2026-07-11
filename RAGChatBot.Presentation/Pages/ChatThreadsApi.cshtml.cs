using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class ChatThreadsApiModel : PageModel
    {
        private readonly IChatService _chatService;

        public ChatThreadsApiModel(IChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task<IActionResult> OnGetAsync(string? courseCode)
        {
            if (!TryGetUserId(out var userId))
            {
                return UnauthorizedResult();
            }

            var threads = await _chatService.GetThreadsAsync(userId, courseCode);
            return new JsonResult(threads.Select(thread => new
            {
                id = thread.Id,
                title = thread.Title,
                courseCode = thread.CourseCode,
                createdAt = thread.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            }));
        }

        public async Task<IActionResult> OnGetMessagesAsync(Guid threadId)
        {
            if (!TryGetUserId(out var userId))
            {
                return UnauthorizedResult();
            }

            var messages = await _chatService.GetMessagesAsync(userId, threadId);
            if (messages is null)
            {
                return new JsonResult(new { error = "Không tìm thấy luồng chat." })
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            return new JsonResult(messages.Select(message => new
            {
                id = message.Id,
                role = message.Role,
                content = message.Content,
                sentAt = message.SentAt.ToString("dd/MM/yyyy HH:mm")
            }));
        }

        private bool TryGetUserId(out Guid userId)
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdValue, out userId);
        }

        private static JsonResult UnauthorizedResult()
            => new(new { error = "Phiên đăng nhập không hợp lệ." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
    }
}
