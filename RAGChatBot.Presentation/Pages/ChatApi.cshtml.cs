using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using RAGChatBot.DAL.Interfaces;
using RAGChatBot.BLL.Services;
using RAGChatBot.DAL.Entities;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using RAGChatBot.DAL.Context;

namespace RAGChatBot.Presentation.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("StudentChatLimit")]
    public class ChatApiModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly ICreditService _creditService;
        private readonly IChatTrackerLogRepository _chatLogRepository;
        private readonly AppDbContext _db;
        private readonly ILogger<ChatApiModel> _logger;

        public ChatApiModel(
            IChatService chatService,
            ICreditService creditService,
            IChatTrackerLogRepository chatLogRepository,
            AppDbContext db,
            ILogger<ChatApiModel> logger)
        {
            _chatService = chatService;
            _creditService = creditService;
            _chatLogRepository = chatLogRepository;
            _db = db;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Message))
            {
                return new JsonResult(new { reply = "Vui lòng nhập câu hỏi." });
            }

            // Lấy userId từ claims
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return new JsonResult(new { reply = "Phiên đăng nhập không hợp lệ." }) { StatusCode = 401 };
            }

            // Kiểm tra và khấu trừ credit
            var (allowed, remaining) = await _creditService.CheckAndDeductCreditAsync(userId);
            if (!allowed)
            {
                return new JsonResult(new
                {
                    reply = "Bạn đã hết lượt hỏi miễn phí hôm nay (10 lượt/ngày). Nâng cấp Premium để chat không giới hạn!",
                    outOfCredits = true,
                    remaining = 0
                });
            }

            try
            {
                // Quản lý hoặc tạo luồng chat (ChatThread)
                Guid activeThreadId;
                if (request.ThreadId.HasValue && request.ThreadId.Value != Guid.Empty)
                {
                    activeThreadId = request.ThreadId.Value;
                }
                else
                {
                    var title = request.Message.Length > 60 ? request.Message.Substring(0, 57) + "..." : request.Message;
                    var newThread = new ChatThread
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        CourseCode = request.CourseCode ?? "General",
                        Title = title,
                        CreatedAt = DateTime.UtcNow.AddHours(7)
                    };
                    _db.ChatThreads.Add(newThread);
                    await _db.SaveChangesAsync();
                    activeThreadId = newThread.Id;
                }

                // Lưu câu hỏi vào database
                var userMsg = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ThreadId = activeThreadId,
                    Role = "user",
                    Content = request.Message,
                    SentAt = DateTime.UtcNow.AddHours(7)
                };
                _db.ChatMessages.Add(userMsg);
                await _db.SaveChangesAsync();

                // Nhận câu trả lời từ RAG Chatbot
                var reply = await _chatService.GetChatResponseAsync(request.Message, request.CourseCode, activeThreadId);

                // Lưu phản hồi chatbot vào database
                var botMsg = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ThreadId = activeThreadId,
                    Role = "assistant",
                    Content = reply,
                    SentAt = DateTime.UtcNow.AddHours(7)
                };
                _db.ChatMessages.Add(botMsg);
                await _db.SaveChangesAsync();

                // Ghi log vào ChatTrackerLogs
                var log = new ChatTrackerLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Question = request.Message,
                    Answer = reply.Length > 2000 ? reply[..2000] : reply, // Giới hạn log answer
                    CourseCode = request.CourseCode,
                    CreatedAt = DateTime.UtcNow
                };
                await _chatLogRepository.AddAsync(log);
                await _chatLogRepository.SaveChangesAsync();

                return new JsonResult(new { reply, remaining, threadId = activeThreadId });
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
            public Guid? ThreadId { get; set; }
        }
    }
}
