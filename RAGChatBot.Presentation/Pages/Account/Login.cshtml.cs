using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IAuthService _authService;
        private readonly IWhitelistService _whitelistService;

        public LoginModel(IAuthService authService, IWhitelistService whitelistService)
        {
            _authService = authService;
            _whitelistService = whitelistService;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ tên tài khoản và mật khẩu!";
                return Page();
            }

            var request = new LoginRequest { Username = Username, Password = Password };
            var userDto = await _authService.LoginAsync(request);
            
            if (userDto == null)
            {
                ErrorMessage = "Tài khoản hoặc mật khẩu không chính xác!";
                return Page();
            }

            await SignInUser(userDto);
            return LocalRedirect(ReturnUrl ?? "/");
        }

        public IActionResult OnPostExternalLogin()
        {
            var redirectUrl = Url.Page("./Login", pageHandler: "ExternalLoginCallback", values: new { ReturnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> OnGetExternalLoginCallback(string? remoteError = null)
        {
            if (remoteError != null)
            {
                ErrorMessage = $"Lỗi từ Google: {remoteError}";
                return Page();
            }

            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                var info = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
                if (!info.Succeeded)
                {
                    ErrorMessage = "Lỗi đăng nhập bằng Google.";
                    return Page();
                }
                result = info;
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                ErrorMessage = "Không lấy được Email từ Google.";
                return Page();
            }

            var userDto = await _authService.GetUserByUsernameAsync(email);
            
            if (userDto == null)
            {
                bool isAllowed = email.EndsWith("@fpt.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 email.EndsWith("@fe.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 await _whitelistService.IsEmailWhitelistedAsync(email);

                if (!isAllowed)
                {
                    ErrorMessage = "Tài khoản của bạn chưa được cấp quyền truy cập hệ thống. Vui lòng liên hệ Admin!";
                    return Page();
                }

                var fullName = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
                var randomPassword = Guid.NewGuid().ToString(); 
                userDto = await _authService.RegisterAsync(email, randomPassword, "Student", "Free", fullName);
            }

            await SignInUser(userDto);
            return LocalRedirect(ReturnUrl ?? "/");
        }

        private async Task SignInUser(UserDto userDto)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userDto.Id.ToString()),
                new Claim(ClaimTypes.Name, !string.IsNullOrEmpty(userDto.FullName) ? userDto.FullName : userDto.Username),
                new Claim(ClaimTypes.Role, userDto.Role),
                new Claim("SubscriptionTier", userDto.SubscriptionTier)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
        }
    }
}
