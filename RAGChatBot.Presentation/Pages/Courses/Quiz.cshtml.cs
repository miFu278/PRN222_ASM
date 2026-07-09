using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RAGChatBot.Presentation.Pages.Courses
{
    [Authorize]
    public class QuizModel : PageModel
    {
        public string CourseCode { get; set; } = string.Empty;

        public void OnGet(string courseCode)
        {
            CourseCode = courseCode;
        }
    }
}
