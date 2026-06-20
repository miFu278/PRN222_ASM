using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System.Security.Claims;

namespace RAGChatBot.WebMVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IWhitelistService _whitelistService;

        public AccountController(IAuthService authService, IWhitelistService whitelistService)
        {
            _authService = authService;
            _whitelistService = whitelistService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            // VÃ´ hiá»‡u hÃ³a Ä‘Äƒng kÃ½ tá»± do ngoÃ i trang login
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult Register(RegisterRequest request)
        {
            // VÃ´ hiá»‡u hÃ³a Ä‘Äƒng kÃ½ tá»± do ngoÃ i trang login
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginRequest request, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                ModelState.AddModelError(string.Empty, "Vui lÃ²ng nháº­p Ä‘áº§y Ä‘á»§ tÃªn tÃ i khoáº£n vÃ  máº­t kháº©u!");
                return View(request);
            }

            var userDto = await _authService.LoginAsync(request);
            if (userDto == null)
            {
                ModelState.AddModelError(string.Empty, "TÃ i khoáº£n hoáº·c máº­t kháº©u khÃ´ng chÃ­nh xÃ¡c!");
                return View(request);
            }

            // Ghi nháº­n thÃ´ng tin Claims
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

            if (userDto.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ExternalLogin(string returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Lá»—i tá»« Google: {remoteError}");
                return View("Login");
            }

            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                // Thá»­ authenticate vá»›i ExternalScheme náº¿u cÃ³, nhÆ°ng á»Ÿ Ä‘Ã¢y ta dÃ¹ng Cookie Auth lÃ m default.
                // Khi callback vá», do ta config AddGoogle mÃ  khÃ´ng dÃ¹ng Identity, properties sáº½ náº±m trong cookie.
                // ThÆ°á»ng thÃ¬ result sáº½ Succeeded náº¿u Google tráº£ vá» Ä‘Ãºng.
                var info = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
                if (!info.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, "Lá»—i Ä‘Äƒng nháº­p báº±ng Google.");
                    return View("Login");
                }
                result = info;
            }

            var email = result.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "KhÃ´ng láº¥y Ä‘Æ°á»£c Email tá»« Google.");
                return View("Login");
            }

            var userDto = await _authService.GetUserByUsernameAsync(email);
            
            if (userDto == null)
            {
                // Kiá»ƒm tra xem email cÃ³ há»£p lá»‡ khÃ´ng (Ä‘uÃ´i FPT hoáº·c náº±m trong Whitelist)
                bool isAllowed = email.EndsWith("@fpt.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 email.EndsWith("@fe.edu.vn", StringComparison.OrdinalIgnoreCase) || 
                                 await _whitelistService.IsEmailWhitelistedAsync(email);

                if (!isAllowed)
                {
                    ModelState.AddModelError(string.Empty, "TÃ i khoáº£n cá»§a báº¡n chÆ°a Ä‘Æ°á»£c cáº¥p quyá»n truy cáº­p há»‡ thá»‘ng. Vui lÃ²ng liÃªn há»‡ Admin!");
                    return View("Login");
                }

                // Tá»± Ä‘á»™ng Ä‘Äƒng kÃ½
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

            // ÄÄƒng nháº­p ngÆ°á»i dÃ¹ng báº±ng cookie cá»§a há»‡ thá»‘ng
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            if (userDto.Role == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}

