using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RAGChatBot.Presentation.Pages.Account
{
    [AllowAnonymous]
    public sealed class AccessDeniedModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
