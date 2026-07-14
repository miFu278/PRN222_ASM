using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
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

        public async Task OnGetAsync()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var userId))
            {
                var userDto = await _authService.GetUserByIdAsync(userId);
                if (userDto != null)
                {
                    // Kiểm tra hết hạn gói Premium
                    if (userDto.SubscriptionTier == "Premium" &&
                        userDto.SubscriptionExpiresAt.HasValue &&
                        userDto.SubscriptionExpiresAt.Value < DateTime.UtcNow)
                    {
                        CurrentTier = "Free";
                    }
                    else
                    {
                        CurrentTier = userDto.SubscriptionTier;
                    }
                    return;
                }
            }
            CurrentTier = User.FindFirst("SubscriptionTier")?.Value ?? "Free"; // fallback
        }
    }
}
