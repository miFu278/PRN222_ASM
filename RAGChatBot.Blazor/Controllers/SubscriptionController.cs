using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RAGChatBot.Blazor.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly IAuthService _authService;

        public SubscriptionController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("Upgrade")]
        public async Task<IActionResult> Upgrade()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return BadRequest("Nhận diện người dùng không hợp lệ.");
            }

            var success = await _authService.UpgradeToPremiumAsync(userId);
            if (!success)
            {
                var errorMsg = Uri.EscapeDataString("Có lỗi xảy ra trong quá trình nâng cấp gói cước.");
                return Redirect($"/subscription?error={errorMsg}");
            }

            // Cập nhật lại Claims trong Cookie để hệ thống nhận diện gói Premium ngay lập tức
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

            var successMsg = Uri.EscapeDataString("Chúc mừng! Bạn đã nâng cấp thành công lên gói Premium (Hạn mức 50MB/file).");
            return Redirect($"/subscription?success={successMsg}");
        }
    }
}
