using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Courses
{
    [Authorize(Roles = RoleNames.Lecturer + "," + RoleNames.Student)]
    public class QuizModel : PageModel
    {
        private readonly ICourseService _courseService;

        public QuizModel(ICourseService courseService)
        {
            _courseService = courseService;
        }

        public string CourseCode { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(string courseCode)
        {
            if (User.IsInRole(RoleNames.Admin)) return Forbid();

            var normalizedCode = courseCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedCode)) return NotFound();

            if (User.IsInRole(RoleNames.Lecturer))
            {
                if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
                    !await _courseService.IsSubjectLeaderAsync(normalizedCode, userId))
                {
                    return Forbid();
                }
            }
            else if (!User.IsInRole(RoleNames.Student))
            {
                return Forbid();
            }

            CourseCode = normalizedCode;
            return Page();
        }
    }
}
