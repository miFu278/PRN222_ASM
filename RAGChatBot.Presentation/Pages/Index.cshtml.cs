using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ICourseService _courseService;

        public IndexModel(ICourseService courseService)
        {
            _courseService = courseService;
        }

        public IReadOnlyList<CourseDto> Courses { get; private set; } = Array.Empty<CourseDto>();

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Admin))
            {
                return RedirectToPage("/Admin/Dashboard");
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                IEnumerable<CourseDto> courses;
                if (User.IsInRole(RoleNames.Lecturer) &&
                    Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var lecturerId))
                {
                    courses = await _courseService.GetCoursesBySubjectLeaderAsync(lecturerId);
                }
                else
                {
                    courses = await _courseService.GetAllCoursesAsync();
                }

                Courses = courses
                    .OrderBy(course => course.Code)
                    .ToList();
            }

            return Page();
        }
    }
}
