using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Subscription
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IAuthService _authService;

        public IndexModel(IAuthService authService)
        {
            _authService = authService;
        }

        [BindProperty(SupportsGet = true)]
        public string? Success { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Error { get; set; }

        public string CurrentTier { get; set; } = "Free";

        public void OnGet()
        {
            CurrentTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";
        }

        public async Task<IActionResult> OnPostUpgradeAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                Error = "Nhận diện người dùng không hợp lệ.";
                return Page();
            }

            var success = await _authService.UpgradeToPremiumAsync(userId);
            if (!success)
            {
                Error = "Có lỗi xảy ra trong quá trình nâng cấp gói cước.";
                return Page();
            }

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

            return RedirectToPage(new { Success = "Chúc mừng! Bạn đã nâng cấp thành công lên gói Premium (Hạn mức 50MB/file)." });
        }
    }
}
