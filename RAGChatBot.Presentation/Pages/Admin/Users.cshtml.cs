using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;

namespace RAGChatBot.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UsersModel : PageModel
    {
        private readonly IAuthService _authService;

        public UsersModel(IAuthService authService)
        {
            _authService = authService;
        }

        public IEnumerable<UserDto> Users { get; set; } = new List<UserDto>();

        [BindProperty]
        public string Username { get; set; } = string.Empty;
        
        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string Role { get; set; } = string.Empty;

        [BindProperty]
        public string SubscriptionTier { get; set; } = string.Empty;

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string DefaultPassword { get; set; } = "Welcome@2026";

        [BindProperty]
        public IFormFile? ImportFile { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                Users = await _authService.GetAllUsersAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách tài khoản: " + ex.Message;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(FullName))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ họ tên, tên tài khoản và mật khẩu!";
                return RedirectToPage();
            }

            try
            {
                await _authService.RegisterAsync(Username.Trim(), Password, Role, SubscriptionTier, FullName.Trim());
                TempData["SuccessMessage"] = $"Đã tạo thành công tài khoản '{FullName}' với vai trò {Role}!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tạo tài khoản: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            try
            {
                await _authService.DeleteUserAsync(id);
                TempData["SuccessMessage"] = "Đã xóa tài khoản thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleTierAsync(Guid id)
        {
            try
            {
                var authServiceConcrete = _authService as AuthService;
                if (authServiceConcrete != null)
                {
                    var success = await authServiceConcrete.ToggleSubscriptionTierAsync(id);
                    if (success)
                    {
                        TempData["SuccessMessage"] = "Đã thay đổi gói cước thành công!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy người dùng!";
                    }
                }
                else
                {
                    var success = await _authService.UpgradeToPremiumAsync(id);
                    if (success) TempData["SuccessMessage"] = "Đã nâng cấp Premium thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể đổi gói cước: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostImportAsync()
        {
            if (ImportFile == null || ImportFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một file Excel (.xlsx hoặc .xls)!";
                return RedirectToPage();
            }

            var ext = Path.GetExtension(ImportFile.FileName).ToLower();
            if (ext != ".xlsx" && ext != ".xls")
            {
                TempData["ErrorMessage"] = "Định dạng file không hợp lệ! Chỉ chấp nhận .xlsx hoặc .xls.";
                return RedirectToPage();
            }

            try
            {
                using var stream = ImportFile.OpenReadStream();
                var (success, skipped) = await _authService.ImportUsersFromExcelAsync(stream, DefaultPassword.Trim());
                TempData["SuccessMessage"] = $"Import thành công {success} tài khoản mới! (Bỏ qua {skipped} tài khoản đã tồn tại). Email đã được gửi đến từng người.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi import: " + ex.Message;
            }

            return RedirectToPage();
        }
    }
}
