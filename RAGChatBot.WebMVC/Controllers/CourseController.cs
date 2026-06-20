using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.WebMVC.Controllers
{
    [Authorize]
    public class CourseController : Controller
    {
        private readonly ICourseService _courseService;
        private readonly IAuthService _authService;

        public CourseController(ICourseService courseService, IAuthService authService)
        {
            _courseService = courseService;
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string search)
        {
            ViewData["SearchKeyword"] = search;
            var courses = await _courseService.SearchCoursesAsync(search);

            if (User.IsInRole("Admin"))
            {
                var users = await _authService.GetAllUsersAsync();
                var lecturers = users.Where(u => u.Role == "Lecturer" || u.Role == "Admin").ToList();
                ViewBag.Lecturers = lecturers;
            }

            return View(courses);
        }

        [HttpGet]
        [Authorize(Roles = "Lecturer,Admin")]
        public async Task<IActionResult> Create()
        {
            var users = await _authService.GetAllUsersAsync();
            var lecturers = users.Where(u => u.Role == "Lecturer" || u.Role == "Admin").ToList();
            ViewBag.Lecturers = lecturers;
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
                ModelState.AddModelError(string.Empty, "Lá»—i Ä‘á»‹nh danh ngÆ°á»i dÃ¹ng.");
                return View(request);
            }

            try
            {
                await _courseService.CreateCourseAsync(request, userId);
                TempData["SuccessMessage"] = $"Táº¡o mÃ´n há»c {request.Code} thÃ nh cÃ´ng!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"CÃ³ lá»—i xáº£y ra: {ex.Message}");
                return View(request);
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignLeader(Guid courseId, Guid subjectLeaderId)
        {
            try
            {
                await _courseService.UpdateSubjectLeaderAsync(courseId, subjectLeaderId);
                TempData["SuccessMessage"] = "ÄÃ£ phÃ¢n cÃ´ng TrÆ°á»Ÿng bá»™ mÃ´n má»›i thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ phÃ¢n cÃ´ng TrÆ°á»Ÿng bá»™ mÃ´n: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(CourseDto request)
        {
            try
            {
                await _courseService.UpdateCourseAsync(request);
                TempData["SuccessMessage"] = $"Cáº­p nháº­t mÃ´n há»c {request.Code} thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ cáº­p nháº­t mÃ´n há»c: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _courseService.DeleteCourseAsync(id);
                TempData["SuccessMessage"] = "ÄÃ£ xÃ³a mÃ´n há»c vÃ  toÃ n bá»™ tÃ i liá»‡u liÃªn quan thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ xÃ³a mÃ´n há»c: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

