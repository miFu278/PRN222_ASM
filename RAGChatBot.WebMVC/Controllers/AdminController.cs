using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Application.Services;
using System;
using System.Threading.Tasks;

namespace RAGChatBot.WebMVC.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IAuthService _authService;
        private readonly IWhitelistService _whitelistService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAuthService authService, IWhitelistService whitelistService, ILogger<AdminController> logger)
        {
            _authService = authService;
            _whitelistService = whitelistService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var users = await _authService.GetAllUsersAsync();
                return View(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi láº¥y danh sÃ¡ch ngÆ°á»i dÃ¹ng cho Admin Dashboard");
                TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ táº£i danh sÃ¡ch tÃ i khoáº£n: " + ex.Message;
                return View(new System.Collections.Generic.List<UserDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(string username, string password, string role, string subscriptionTier, string fullName)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
            {
                TempData["ErrorMessage"] = "Vui lÃ²ng nháº­p Ä‘áº§y Ä‘á»§ há» tÃªn, tÃªn tÃ i khoáº£n vÃ  máº­t kháº©u!";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _authService.RegisterAsync(username.Trim(), password, role, subscriptionTier, fullName.Trim());
                TempData["SuccessMessage"] = $"ÄÃ£ táº¡o thÃ nh cÃ´ng tÃ i khoáº£n '{fullName}' vá»›i vai trÃ² {role}!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi Admin táº¡o tÃ i khoáº£n {Username}", username);
                TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ táº¡o tÃ i khoáº£n: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _authService.DeleteUserAsync(id);
                TempData["SuccessMessage"] = "ÄÃ£ xÃ³a tÃ i khoáº£n thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi xÃ³a tÃ i khoáº£n {UserId}", id);
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTier(Guid id)
        {
            try
            {
                // Gá»i API chuyá»ƒn Ä‘á»•i nhanh gÃ³i cÆ°á»›c
                var authServiceConcrete = _authService as AuthService;
                if (authServiceConcrete != null)
                {
                    var success = await authServiceConcrete.ToggleSubscriptionTierAsync(id);
                    if (success)
                    {
                        TempData["SuccessMessage"] = "ÄÃ£ thay Ä‘á»•i gÃ³i cÆ°á»›c thÃ nh cÃ´ng!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "KhÃ´ng tÃ¬m tháº¥y ngÆ°á»i dÃ¹ng!";
                    }
                }
                else
                {
                    // Fallback
                    var success = await _authService.UpgradeToPremiumAsync(id);
                    if (success) TempData["SuccessMessage"] = "ÄÃ£ nÃ¢ng cáº¥p Premium thÃ nh cÃ´ng!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi chuyá»ƒn Ä‘á»•i gÃ³i cÆ°á»›c cho tÃ i khoáº£n {UserId}", id);
                TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ Ä‘á»•i gÃ³i cÆ°á»›c: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ImportUsers(Microsoft.AspNetCore.Http.IFormFile file, string defaultPassword)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lÃ²ng chá»n má»™t file Excel (.xlsx hoáº·c .xls)!";
                return RedirectToAction(nameof(Index));
            }

            var ext = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (ext != ".xlsx" && ext != ".xls")
            {
                TempData["ErrorMessage"] = "Äá»‹nh dáº¡ng file khÃ´ng há»£p lá»‡! Chá»‰ cháº¥p nháº­n .xlsx hoáº·c .xls.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(defaultPassword))
                defaultPassword = "Welcome@2026";

            try
            {
                using var stream = file.OpenReadStream();
                var (success, skipped) = await _authService.ImportUsersFromExcelAsync(stream, defaultPassword.Trim());
                TempData["SuccessMessage"] = $"Import thÃ nh cÃ´ng {success} tÃ i khoáº£n má»›i! (Bá» qua {skipped} tÃ i khoáº£n Ä‘Ã£ tá»“n táº¡i). Email Ä‘Ã£ Ä‘Æ°á»£c gá»­i Ä‘áº¿n tá»«ng ngÆ°á»i.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi import file Excel tÃ i khoáº£n ngÆ°á»i dÃ¹ng");
                TempData["ErrorMessage"] = "Lá»—i khi import: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Whitelist()
        {
            try
            {
                var whitelist = await _whitelistService.GetAllAsync();
                return View(whitelist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi láº¥y danh sÃ¡ch Whitelist cho Admin Dashboard");
                TempData["ErrorMessage"] = "KhÃ´ng thá»ƒ táº£i danh sÃ¡ch Whitelist: " + ex.Message;
                return View(new System.Collections.Generic.List<WhitelistEmailDto>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddToWhitelist(string email, string? fullName, string? studentId)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Email khÃ´ng Ä‘Æ°á»£c Ä‘á»ƒ trá»‘ng!";
                return RedirectToAction(nameof(Whitelist));
            }

            try
            {
                await _whitelistService.AddAsync(email.Trim(), fullName, studentId);
                TempData["SuccessMessage"] = $"ÄÃ£ thÃªm email '{email}' vÃ o Whitelist thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi Admin thÃªm email vÃ o whitelist: {Email}", email);
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Whitelist));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFromWhitelist(Guid id)
        {
            try
            {
                await _whitelistService.DeleteAsync(id);
                TempData["SuccessMessage"] = "ÄÃ£ xÃ³a email khá»i Whitelist thÃ nh cÃ´ng!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi xÃ³a email khá»i whitelist: {Id}", id);
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Whitelist));
        }

        [HttpPost]
        public async Task<IActionResult> ImportWhitelist(Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lÃ²ng chá»n má»™t file Excel (.xlsx hoáº·c .xls)!";
                return RedirectToAction(nameof(Whitelist));
            }

            var extension = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                TempData["ErrorMessage"] = "Äá»‹nh dáº¡ng file khÃ´ng Ä‘Æ°á»£c há»— trá»£! Vui lÃ²ng chá»n file Excel (.xlsx hoáº·c .xls).";
                return RedirectToAction(nameof(Whitelist));
            }

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var count = await _whitelistService.ImportFromExcelAsync(stream);
                    TempData["SuccessMessage"] = $"Import thÃ nh cÃ´ng {count} email vÃ o danh sÃ¡ch Whitelist!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lá»—i khi import file whitelist Excel");
                TempData["ErrorMessage"] = "Lá»—i khi import file Excel: " + ex.Message;
            }

            return RedirectToAction(nameof(Whitelist));
        }
    }
}

