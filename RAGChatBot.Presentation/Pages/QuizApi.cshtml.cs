using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.DAL.Entities;
using RAGChatBot.DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class QuizApiModel : PageModel
    {
        private readonly IQuizService _quizService;

        public QuizApiModel(IQuizService quizService)
        {
            _quizService = quizService;
        }

        public async Task<IActionResult> OnGetAsync(string courseCode)
        {
            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return new JsonResult(new { error = "Mã môn học không được để trống." }) { StatusCode = 400 };
            }

            var quiz = await _quizService.GetQuizByCourseAsync(courseCode);

            // Bảo mật: Không trả về CorrectAnswer về client để tránh gian lận
            var safeQuiz = quiz.Select(q => new
            {
                id = q.Id,
                questionText = q.QuestionText,
                optionA = q.OptionA,
                optionB = q.OptionB,
                optionC = q.OptionC,
                optionD = q.OptionD
            });

            return new JsonResult(safeQuiz);
        }

        public async Task<IActionResult> OnPostSubmitAsync([FromBody] QuizSubmitRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CourseCode) || request.Answers == null)
            {
                return new JsonResult(new { error = "Dữ liệu nộp bài không hợp lệ." }) { StatusCode = 400 };
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return new JsonResult(new { error = "Phiên đăng nhập không hợp lệ." }) { StatusCode = 401 };
            }

            try
            {
                var attempt = await _quizService.SubmitQuizAttemptAsync(userId, request.CourseCode, request.Answers);

                return new JsonResult(new
                {
                    success = true,
                    score = attempt.Score,
                    totalQuestions = attempt.TotalQuestions,
                    percentage = attempt.Percentage,
                    attemptedAt = attempt.AttemptedAt.ToString("dd/MM/yyyy HH:mm")
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = $"Lỗi khi chấm điểm bài thi: {ex.Message}" }) { StatusCode = 500 };
            }
        }

        public class QuizSubmitRequest
        {
            public string CourseCode { get; set; } = string.Empty;
            public List<UserAnswerDto> Answers { get; set; } = new();
        }
    }
}
