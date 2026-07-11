using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGChatBot.Domain.Constants;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;

namespace RAGChatBot.Presentation.Pages.Admin
{
    [Authorize(Roles = RoleNames.Admin)]
    public class WhitelistModel : PageModel
    {
        private readonly IWhitelistService _whitelistService;

        public WhitelistModel(IWhitelistService whitelistService)
        {
            _whitelistService = whitelistService;
        }

        public IEnumerable<WhitelistEmailDto> WhitelistEmails { get; set; } = new List<WhitelistEmailDto>();

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string? FullName { get; set; }

        [BindProperty]
        public string? StudentId { get; set; }

        [BindProperty]
        public IFormFile? ImportFile { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                WhitelistEmails = await _whitelistService.GetAllAsync();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể tải danh sách Whitelist: " + ex.Message;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                TempData["ErrorMessage"] = "Email không được để trống!";
                return RedirectToPage();
            }

            try
            {
                await _whitelistService.AddAsync(Email.Trim(), FullName, StudentId);
                TempData["SuccessMessage"] = $"Đã thêm email '{Email}' vào Whitelist thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(Guid id)
        {
            try
            {
                await _whitelistService.DeleteAsync(id);
                TempData["SuccessMessage"] = "Đã xóa email khỏi Whitelist thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
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

            var extension = Path.GetExtension(ImportFile.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                TempData["ErrorMessage"] = "Định dạng file không được hỗ trợ! Vui lòng chọn file Excel (.xlsx hoặc .xls).";
                return RedirectToPage();
            }

            try
            {
                using var stream = ImportFile.OpenReadStream();
                var count = await _whitelistService.ImportFromExcelAsync(stream);
                TempData["SuccessMessage"] = $"Import thành công {count} email vào danh sách Whitelist!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi import file Excel: " + ex.Message;
            }

            return RedirectToPage();
        }
    }
}
