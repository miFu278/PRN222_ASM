using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Application.Services;
using RAGChatBot.Application.DTOs;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Courses
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ICourseService _courseService;
        private readonly IAuthService _authService;

        public IndexModel(ICourseService courseService, IAuthService authService)
        {
            _courseService = courseService;
            _authService = authService;
        }

        public IEnumerable<CourseDto> Courses { get; set; } = new List<CourseDto>();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public IEnumerable<UserDto> Lecturers { get; set; } = new List<UserDto>();

        [BindProperty]
        public Guid? Id { get; set; }

        [BindProperty]
        public string Code { get; set; } = string.Empty;

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public Guid SubjectLeaderId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userIdStr, out var currentUserId);

            if (User.IsInRole("Lecturer"))
            {
                Courses = await _courseService.GetCoursesBySubjectLeaderAsync(currentUserId);
            }
            else
            {
                Courses = await _courseService.GetAllCoursesAsync();
            }

            if (!string.IsNullOrEmpty(Search))
            {
                Courses = Courses.Where(c => 
                    c.Code.Contains(Search, StringComparison.OrdinalIgnoreCase) || 
                    c.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
            }

            if (User.IsInRole("Admin"))
            {
                var users = await _authService.GetAllUsersAsync();
                Lecturers = users.Where(u => u.Role == "Lecturer").Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Role = u.Role
                });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            if (!User.IsInRole("Admin")) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToPage();
            }

            try
            {
                if (Id.HasValue && Id.Value != Guid.Empty)
                {
                    var dto = new CourseDto { Id = Id.Value, Code = Code, Name = Name, Description = Description, SubjectLeaderId = SubjectLeaderId };
                    await _courseService.UpdateCourseAsync(dto);
                    TempData["SuccessMessage"] = $"Đã cập nhật môn {Code}.";
                }
                else
                {
                    var existingCourses = await _courseService.GetAllCoursesAsync();
                    if (existingCourses.Any(c => c.Code.Equals(Code, StringComparison.OrdinalIgnoreCase)))
                    {
                        TempData["ErrorMessage"] = $"Mã môn {Code} đã tồn tại!";
                        return RedirectToPage();
                    }

                    var dto = new CourseDto { Code = Code, Name = Name, Description = Description, SubjectLeaderId = SubjectLeaderId };
                    var currentUserIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    Guid.TryParse(currentUserIdStr, out var currentUserId);
                    await _courseService.CreateCourseAsync(dto, currentUserId);
                    TempData["SuccessMessage"] = $"Đã tạo môn mới {Code}.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            if (!User.IsInRole("Admin")) return Forbid();

            try
            {
                await _courseService.DeleteCourseAsync(id);
                TempData["SuccessMessage"] = "Đã xóa môn học.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi xóa môn: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
