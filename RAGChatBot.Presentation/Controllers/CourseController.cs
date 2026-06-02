using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Controllers
{
    [Authorize]
    public class CourseController : Controller
    {
        private readonly ICourseService _courseService;

        public CourseController(ICourseService courseService)
        {
            _courseService = courseService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string search)
        {
            ViewData["SearchKeyword"] = search;
            var courses = await _courseService.SearchCoursesAsync(search);
            return View(courses);
        }

        [HttpGet]
        [Authorize(Roles = "Lecturer,Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Create(CourseDto request)
        {
            if (!ModelState.IsValid)
            {
                return View(request);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                ModelState.AddModelError(string.Empty, "Lỗi định danh người dùng.");
                return View(request);
            }

            try
            {
                await _courseService.CreateCourseAsync(request, userId);
                TempData["SuccessMessage"] = $"Tạo môn học {request.Code} thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Có lỗi xảy ra: {ex.Message}");
                return View(request);
            }
        }
    }
}
