using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.DAL.Entities;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class ChatThreadsApiModel : PageModel
    {
        private readonly AppDbContext _db;

        public ChatThreadsApiModel(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> OnGetAsync(string? courseCode)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return new JsonResult(new { error = "Phiên đăng nhập không hợp lệ." }) { StatusCode = 401 };
            }

            var query = _db.ChatThreads.Where(t => t.UserId == userId);
            if (!string.IsNullOrWhiteSpace(courseCode))
            {
                query = query.Where(t => t.CourseCode.ToLower() == courseCode.ToLower());
            }

            var threads = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    courseCode = t.CourseCode,
                    createdAt = t.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return new JsonResult(threads);
        }

        public async Task<IActionResult> OnGetMessagesAsync(Guid threadId)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return new JsonResult(new { error = "Phiên đăng nhập không hợp lệ." }) { StatusCode = 401 };
            }

            var thread = await _db.ChatThreads.FindAsync(threadId);
            if (thread == null || thread.UserId != userId)
            {
                return new JsonResult(new { error = "Không tìm thấy luồng chat." }) { StatusCode = 404 };
            }

            var messages = await _db.ChatMessages
                .Where(m => m.ThreadId == threadId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    id = m.Id,
                    role = m.Role,
                    content = m.Content,
                    sentAt = m.SentAt.ToString("dd/MM/yyyy HH:mm")
                })
                .ToListAsync();

            return new JsonResult(messages);
        }
    }
}
