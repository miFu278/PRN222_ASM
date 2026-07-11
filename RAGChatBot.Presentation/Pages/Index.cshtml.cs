using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Domain.Constants;

namespace RAGChatBot.Presentation.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Admin))
            {
                return RedirectToPage("/Admin/Dashboard");
            }
            return Page();
        }
    }
}
