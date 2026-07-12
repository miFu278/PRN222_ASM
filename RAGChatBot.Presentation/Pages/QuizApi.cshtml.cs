using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Enums;
using RAGChatBot.BLL.Services;
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
        private readonly IDocumentService _documentService;

        public QuizApiModel(IQuizService quizService, IDocumentService documentService)
        {
            _quizService = quizService;
            _documentService = documentService;
        }

        public async Task<IActionResult> OnGetAsync(string courseCode)
        {
            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return new JsonResult(new { error = "Mã môn học không được để trống." }) { StatusCode = 400 };
            }

            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);

            if (isLecturerOrAdmin)
            {
                var questions = await _quizService.GetQuestionBankByCourseAsync(courseCode);
                return new JsonResult(questions);
            }
            else
            {
                // Học sinh mặc định sẽ tải danh sách các đề thi (Quiz) thay vì tự động tải câu hỏi
                var quizzes = await _quizService.GetQuizzesByCourseAsync(courseCode);
                return new JsonResult(quizzes);
            }
        }

        public async Task<IActionResult> OnGetQuizzesAsync(string courseCode)
        {
            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return new JsonResult(new { error = "Mã môn học không được để trống." }) { StatusCode = 400 };
            }

            var quizzes = await _quizService.GetQuizzesByCourseAsync(courseCode);
            return new JsonResult(quizzes);
        }

        public async Task<IActionResult> OnGetQuizQuestionsAsync(Guid quizId)
        {
            if (quizId == Guid.Empty)
            {
                return new JsonResult(new { error = "Mã bài trắc nghiệm không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                var questions = await _quizService.GetQuizQuestionsAsync(quizId);
                return new JsonResult(questions);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetDocumentsAsync(string courseCode)
        {
            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return new JsonResult(new { error = "Mã môn học không được để trống." }) { StatusCode = 400 };
            }

            var documents = await _documentService.GetDocumentsByCourseAsync(courseCode);
            var approvedDocs = documents
                .Where(d => d.Status == DocumentStatus.Success && d.IsApproved)
                .Select(d => new
                {
                    id = d.Id,
                    fileName = d.FileName,
                    chapter = d.Chapter
                })
                .ToList();

            return new JsonResult(approvedDocs);
        }

        public async Task<IActionResult> OnGetAttemptsAsync(string courseCode)
        {
            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);
            if (!isLecturerOrAdmin)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(courseCode))
            {
                return new JsonResult(new { error = "Mã môn học không được để trống." }) { StatusCode = 400 };
            }

            var attempts = await _quizService.GetAttemptsByCourseAsync(courseCode);
            return new JsonResult(attempts);
        }

        public async Task<IActionResult> OnPostSubmitAsync([FromBody] QuizSubmitRequest? request)
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
                var attempt = await _quizService.SubmitQuizAttemptAsync(userId, request.CourseCode, request.QuizId, request.Answers);

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

        public async Task<IActionResult> OnPostCreateQuizAsync([FromBody] CreateQuizRequest? request)
        {
            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);
            if (!isLecturerOrAdmin)
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.CourseCode) || string.IsNullOrWhiteSpace(request.Title) || request.QuestionCount <= 0)
            {
                return new JsonResult(new { error = "Dữ liệu tạo đề thi không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                var quiz = await _quizService.CreateQuizAsync(request.CourseCode, request.Title, request.QuestionCount, request.DocumentId);
                return new JsonResult(new { success = true, id = quiz.Id });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostDeleteQuizAsync([FromBody] DeleteQuizRequest? request)
        {
            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);
            if (!isLecturerOrAdmin)
            {
                return Forbid();
            }

            if (request == null || request.Id == Guid.Empty)
            {
                return new JsonResult(new { error = "Mã đề thi không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                await _quizService.DeleteQuizAsync(request.Id);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostAddQuestionAsync([FromBody] AddQuestionRequest? request)
        {
            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);
            if (!isLecturerOrAdmin)
            {
                return Forbid();
            }

            if (request == null || string.IsNullOrWhiteSpace(request.CourseCode) || string.IsNullOrWhiteSpace(request.QuestionText))
            {
                return new JsonResult(new { error = "Dữ liệu câu hỏi không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                var question = new QuestionBank
                {
                    CourseCode = request.CourseCode,
                    QuestionText = request.QuestionText,
                    OptionA = request.OptionA,
                    OptionB = request.OptionB,
                    OptionC = request.OptionC,
                    OptionD = request.OptionD,
                    CorrectAnswer = request.CorrectAnswer
                };

                await _quizService.AddQuestionAsync(question);
                return new JsonResult(new { success = true, id = question.Id });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostUpdateQuestionAsync([FromBody] UpdateQuestionRequest? request)
        {
            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);
            if (!isLecturerOrAdmin)
            {
                return Forbid();
            }

            if (request == null || request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.QuestionText))
            {
                return new JsonResult(new { error = "Dữ liệu cập nhật không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                var question = new QuestionBank
                {
                    Id = request.Id,
                    QuestionText = request.QuestionText,
                    OptionA = request.OptionA,
                    OptionB = request.OptionB,
                    OptionC = request.OptionC,
                    OptionD = request.OptionD,
                    CorrectAnswer = request.CorrectAnswer
                };

                await _quizService.UpdateQuestionAsync(question);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostDeleteQuestionAsync([FromBody] DeleteQuestionRequest? request)
        {
            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);
            if (!isLecturerOrAdmin)
            {
                return Forbid();
            }

            if (request == null || request.Id == Guid.Empty)
            {
                return new JsonResult(new { error = "Mã câu hỏi không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                await _quizService.DeleteQuestionAsync(request.Id);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostGenerateAsync([FromBody] GenerateRequest? request)
        {
            var isLecturerOrAdmin = User.IsInRole(RoleNames.Lecturer) || User.IsInRole(RoleNames.Admin);
            if (!isLecturerOrAdmin)
            {
                return Forbid();
            }

            if (request == null || request.DocumentId == Guid.Empty)
            {
                return new JsonResult(new { error = "Mã tài liệu không hợp lệ." }) { StatusCode = 400 };
            }

            try
            {
                await _quizService.GenerateQuizForDocumentAsync(request.DocumentId);
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        public class QuizSubmitRequest
        {
            public string CourseCode { get; set; } = string.Empty;
            public Guid? QuizId { get; set; }
            public List<UserAnswerDto> Answers { get; set; } = new();
        }

        public class CreateQuizRequest
        {
            public string CourseCode { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public int QuestionCount { get; set; }
            public Guid? DocumentId { get; set; }
        }

        public class DeleteQuizRequest
        {
            public Guid Id { get; set; }
        }

        public class AddQuestionRequest
        {
            public string CourseCode { get; set; } = string.Empty;
            public string QuestionText { get; set; } = string.Empty;
            public string OptionA { get; set; } = string.Empty;
            public string OptionB { get; set; } = string.Empty;
            public string OptionC { get; set; } = string.Empty;
            public string OptionD { get; set; } = string.Empty;
            public string CorrectAnswer { get; set; } = string.Empty;
        }

        public class UpdateQuestionRequest
        {
            public Guid Id { get; set; }
            public string QuestionText { get; set; } = string.Empty;
            public string OptionA { get; set; } = string.Empty;
            public string OptionB { get; set; } = string.Empty;
            public string OptionC { get; set; } = string.Empty;
            public string OptionD { get; set; } = string.Empty;
            public string CorrectAnswer { get; set; } = string.Empty;
        }

        public class DeleteQuestionRequest
        {
            public Guid Id { get; set; }
        }

        public class GenerateRequest
        {
            public Guid DocumentId { get; set; }
        }
    }
}
