using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly IAuthService _authService;

        public SubscriptionController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";
            ViewBag.CurrentTier = userTier;
            return View();
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            var userTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";
            if (userTier == "Premium")
            {
                TempData["SuccessMessage"] = "Bạn đã ở gói Premium rồi, không cần đăng ký thêm!";
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return BadRequest("Invalid user.");
            }

            // Thực hiện nghiệp vụ nâng cấp gói
            var success = await _authService.UpgradeToPremiumAsync(userId);
            if (!success)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi nâng cấp gói cước.";
                return RedirectToAction("Index");
            }

            // Cập nhật lại Claims trong Cookie để hệ thống nhận diện gói Premium ngay
            var currentClaims = User.Claims.ToList();
            var tierClaim = currentClaims.FirstOrDefault(c => c.Type == "SubscriptionTier");
            if (tierClaim != null)
            {
                currentClaims.Remove(tierClaim);
            }
            currentClaims.Add(new Claim("SubscriptionTier", "Premium"));

            var claimsIdentity = new ClaimsIdentity(currentClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            TempData["SuccessMessage"] = "🎉 Chúc mừng! Bạn đã nâng cấp thành công lên gói Premium (Giới hạn 50MB/file).";
            return RedirectToAction("Index", "Document");
        }
    }
}
