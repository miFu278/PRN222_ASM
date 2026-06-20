using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.WebMVC.Controllers
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
                TempData["SuccessMessage"] = "Báº¡n Ä‘Ã£ á»Ÿ gÃ³i Premium rá»“i, khÃ´ng cáº§n Ä‘Äƒng kÃ½ thÃªm!";
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

            // Thá»±c hiá»‡n nghiá»‡p vá»¥ nÃ¢ng cáº¥p gÃ³i
            var success = await _authService.UpgradeToPremiumAsync(userId);
            if (!success)
            {
                TempData["ErrorMessage"] = "CÃ³ lá»—i xáº£y ra khi nÃ¢ng cáº¥p gÃ³i cÆ°á»›c.";
                return RedirectToAction("Index");
            }

            // Cáº­p nháº­t láº¡i Claims trong Cookie Ä‘á»ƒ há»‡ thá»‘ng nháº­n diá»‡n gÃ³i Premium ngay
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

            TempData["SuccessMessage"] = "ðŸŽ‰ ChÃºc má»«ng! Báº¡n Ä‘Ã£ nÃ¢ng cáº¥p thÃ nh cÃ´ng lÃªn gÃ³i Premium (Giá»›i háº¡n 50MB/file).";
            return RedirectToAction("Index", "Document");
        }
    }
}

