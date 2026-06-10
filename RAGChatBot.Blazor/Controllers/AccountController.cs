using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RAGChatBot.Blazor.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IWhitelistService _whitelistService;

        public AccountController(IAuthService authService, IWhitelistService whitelistService)
        {
            _authService = authService;
            _whitelistService = whitelistService;
        }

        [HttpPost("PerformLogin")]
        public async Task<IActionResult> Login([FromForm] LoginRequest request, [FromQuery] string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                var errorMsg = Uri.EscapeDataString("Vui lòng nhập đầy đủ tên tài khoản và mật khẩu!");
                return Redirect($"/Account/Login?error={errorMsg}&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
            }

            var userDto = await _authService.LoginAsync(request);
            if (userDto == null)
            {
                var errorMsg = Uri.EscapeDataString("Tài khoản hoặc mật khẩu không chính xác!");
                return Redirect($"/Account/Login?error={errorMsg}&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
            }

            // Ghi nhận thông tin Claims
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

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect("/");
        }

        [HttpGet("Logout")]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/Account/Login");
        }

        [HttpPost("ExternalLogin")]
        public IActionResult ExternalLogin([FromForm] string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("ExternalLoginCallback")]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
            {
                var errorMsg = Uri.EscapeDataString($"Lỗi từ Google: {remoteError}");
                return Redirect($"/Account/Login?error={errorMsg}&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
            }

            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                var info = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
                if (!info.Succeeded)
                {
                    var errorMsg = Uri.EscapeDataString("Lỗi đăng nhập bằng Google.");
                    return Redirect($"/Account/Login?error={errorMsg}&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
                }
                result = info;
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                var errorMsg = Uri.EscapeDataString("Không lấy được Email từ Google.");
                return Redirect($"/Account/Login?error={errorMsg}&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
            }

            var userDto = await _authService.GetUserByUsernameAsync(email);
            
            if (userDto == null)
            {
                // Kiểm tra xem email có hợp lệ không (đuôi FPT hoặc nằm trong Whitelist)
                bool isAllowed = email.EndsWith("@fpt.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 email.EndsWith("@fe.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 await _whitelistService.IsEmailWhitelistedAsync(email);

                if (!isAllowed)
                {
                    var errorMsg = Uri.EscapeDataString("Tài khoản của bạn chưa được cấp quyền truy cập hệ thống. Vui lòng liên hệ Admin!");
                    return Redirect($"/Account/Login?error={errorMsg}&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
                }

                // Tự động đăng ký
                var fullName = result.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
                var randomPassword = Guid.NewGuid().ToString(); // Google users won't use password
                userDto = await _authService.RegisterAsync(email, randomPassword, "Student", "Free", fullName);
            }

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

            // Đăng nhập người dùng bằng cookie của hệ thống
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect("/");
        }
    }
}
