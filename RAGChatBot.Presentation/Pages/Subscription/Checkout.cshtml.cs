using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Application.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Pages.Subscription
{
    [Authorize]
    public class CheckoutModel : PageModel
    {
        private readonly IAuthService _authService;

        public CheckoutModel(IAuthService authService)
        {
            _authService = authService;
        }

        [BindProperty]
        public string CardHolderName { get; set; } = string.Empty;

        [BindProperty]
        public string CardNumber { get; set; } = string.Empty;

        [BindProperty]
        public string ExpiryDate { get; set; } = string.Empty;

        [BindProperty]
        public string Cvv { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            var userTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";
            if (userTier == "Premium")
            {
                TempData["SuccessMessage"] = "Bạn đã ở gói Premium rồi, không cần đăng ký thêm!";
                return RedirectToPage("/Index");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free";
            if (userTier == "Premium")
            {
                return RedirectToPage("/Index");
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                ErrorMessage = "Nhận diện người dùng không hợp lệ.";
                return Page();
            }

            // Nghiệp vụ nâng cấp gói cước
            var success = await _authService.UpgradeToPremiumAsync(userId);
            if (!success)
            {
                ErrorMessage = "Có lỗi xảy ra trong quá trình xử lý nâng cấp gói cước.";
                return Page();
            }

            // Cập nhật lại Claims trong Cookie
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

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme, 
                new ClaimsPrincipal(claimsIdentity), 
                authProperties
            );

            TempData["SuccessMessage"] = "Chúc mừng! Bạn đã nâng cấp thành công lên gói Premium (Hạn mức 50MB/file).";
            return RedirectToPage("/Subscription/Index");
        }
    }
}
