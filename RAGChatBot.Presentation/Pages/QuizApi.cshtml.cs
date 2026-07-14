using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Enums;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Pages
{
    [Authorize]
    public class QuizApiModel : PageModel
    {
        private readonly IQuizService _quizService;
        private readonly IDocumentService _documentService;
        private readonly ICourseService _courseService;

        public QuizApiModel(IQuizService quizService, IDocumentService documentService, ICourseService courseService)
        {
            _quizService = quizService;
            _documentService = documentService;
            _courseService = courseService;
        }

        public async Task<IActionResult> OnGetAsync(string courseCode)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (string.IsNullOrWhiteSpace(courseCode)) return BadRequestJson("Mã môn học không hợp lệ.");

            if (await CanManageCourseAsync(courseCode, userId))
                return new JsonResult(await _quizService.GetQuestionBankByCourseAsync(courseCode));

            return new JsonResult(await _quizService.GetQuizzesByCourseAsync(courseCode, userId));
        }

        public async Task<IActionResult> OnGetQuizzesAsync(string courseCode)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (string.IsNullOrWhiteSpace(courseCode)) return BadRequestJson("Mã môn học không hợp lệ.");
            var canManage = await CanManageCourseAsync(courseCode, userId);
            return new JsonResult(await _quizService.GetQuizzesByCourseAsync(courseCode, canManage ? null : userId));
        }

        public async Task<IActionResult> OnGetDocumentsAsync(string courseCode)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (!await CanManageCourseAsync(courseCode, userId)) return Forbid();
            var documents = await _documentService.GetDocumentsByCourseAsync(courseCode);
            return new JsonResult(documents
                .Where(document => document.Status == DocumentStatus.Success && document.IsApproved)
                .Select(document => new { id = document.Id, fileName = document.FileName, chapter = document.Chapter }));
        }

        public async Task<IActionResult> OnGetAttemptsAsync(string courseCode)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (!await CanManageCourseAsync(courseCode, userId)) return Forbid();
            return new JsonResult(await _quizService.GetAttemptsByCourseAsync(courseCode));
        }

        public async Task<IActionResult> OnGetMyAttemptsAsync(string? courseCode)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            return new JsonResult(await _quizService.GetStudentAttemptsAsync(userId, courseCode));
        }

        public async Task<IActionResult> OnGetAttemptReviewAsync(Guid attemptId, string? courseCode)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            var instructorView = !string.IsNullOrWhiteSpace(courseCode) && await CanManageCourseAsync(courseCode, userId);
            return await ExecuteAsync(async () => new JsonResult(
                await _quizService.GetAttemptReviewAsync(userId, attemptId, instructorView, instructorView ? courseCode : null)));
        }

        public async Task<IActionResult> OnPostStartAttemptAsync([FromBody] StartAttemptRequest? request)
        {
            if (!User.IsInRole(RoleNames.Student)) return Forbid();
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || request.QuizId == Guid.Empty) return BadRequestJson("Bài trắc nghiệm không hợp lệ.");
            return await ExecuteAsync(async () =>
            {
                var result = await _quizService.StartQuizAttemptAsync(userId, request.QuizId, request.Password);
                return new JsonResult(new
                {
                    success = true,
                    attemptId = result.AttemptId,
                    attemptNumber = result.AttemptNumber,
                    expiresAt = result.ExpiresAt,
                    questions = result.Questions
                });
            });
        }

        public async Task<IActionResult> OnPostSubmitAsync([FromBody] QuizSubmitRequest? request)
        {
            if (!User.IsInRole(RoleNames.Student)) return Forbid();
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || request.AttemptId == Guid.Empty || request.Answers is null)
                return BadRequestJson("Dữ liệu nộp bài không hợp lệ.");

            return await ExecuteAsync(async () =>
            {
                var result = await _quizService.SubmitQuizAttemptAsync(userId, request.AttemptId, request.Answers);
                return new JsonResult(new
                {
                    success = true,
                    attemptId = result.AttemptId,
                    score = result.Score,
                    totalQuestions = result.TotalQuestions,
                    percentage = result.Percentage,
                    attemptedAt = result.AttemptedAt
                });
            });
        }

        public async Task<IActionResult> OnPostCreateQuizAsync([FromBody] CreateQuizRequest? request)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || !await CanManageCourseAsync(request.CourseCode, userId)) return Forbid();
            return await ExecuteAsync(async () =>
            {
                var quiz = await _quizService.CreateQuizAsync(
                    request.CourseCode, request.Title, request.QuestionCount, request.DocumentId,
                    request.MaxAttempts, request.DurationMinutes, request.Password, request.ReviewPolicy,
                    request.ShuffleQuestions, request.ShuffleOptions);
                return new JsonResult(new { success = true, id = quiz.Id });
            });
        }

        public async Task<IActionResult> OnPostDeleteQuizAsync([FromBody] DeleteQuizRequest? request)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || request.Id == Guid.Empty || !await CanManageCourseAsync(request.CourseCode, userId)) return Forbid();
            await _quizService.DeleteQuizAsync(request.Id, request.CourseCode);
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostAddQuestionAsync([FromBody] AddQuestionRequest? request)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || !await CanManageCourseAsync(request.CourseCode, userId)) return Forbid();
            return await ExecuteAsync(async () =>
            {
                var question = await _quizService.AddQuestionAsync(ToQuestion(request));
                return new JsonResult(new { success = true, id = question.Id });
            });
        }

        public async Task<IActionResult> OnPostUpdateQuestionAsync([FromBody] UpdateQuestionRequest? request)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || request.Id == Guid.Empty || !await CanManageCourseAsync(request.CourseCode, userId)) return Forbid();
            return await ExecuteAsync(async () =>
            {
                await _quizService.UpdateQuestionAsync(ToQuestion(request, request.Id));
                return new JsonResult(new { success = true });
            });
        }

        public async Task<IActionResult> OnPostDeleteQuestionAsync([FromBody] DeleteQuestionRequest? request)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || request.Id == Guid.Empty || !await CanManageCourseAsync(request.CourseCode, userId)) return Forbid();
            await _quizService.DeleteQuestionAsync(request.Id, request.CourseCode);
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostGenerateAsync([FromBody] GenerateRequest? request)
        {
            if (!TryGetUserId(out var userId)) return UnauthorizedJson();
            if (request is null || request.DocumentId == Guid.Empty) return BadRequestJson("Tài liệu không hợp lệ.");
            var document = await _documentService.GetDocumentByIdAsync(request.DocumentId);
            if (document is null || !await CanManageCourseAsync(document.CourseCode, userId)) return Forbid();
            await _quizService.GenerateQuizForDocumentAsync(request.DocumentId);
            return new JsonResult(new { success = true });
        }

        private Task<bool> CanManageCourseAsync(string courseCode, Guid userId)
            => User.IsInRole(RoleNames.Admin)
                ? Task.FromResult(true)
                : User.IsInRole(RoleNames.Lecturer)
                    ? _courseService.IsSubjectLeaderAsync(courseCode, userId)
                    : Task.FromResult(false);

        private bool TryGetUserId(out Guid userId)
            => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        private static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
        {
            try { return await action(); }
            catch (UnauthorizedAccessException exception) { return new JsonResult(new { error = exception.Message }) { StatusCode = 403 }; }
            catch (KeyNotFoundException exception) { return new JsonResult(new { error = exception.Message }) { StatusCode = 404 }; }
            catch (ArgumentException exception) { return new JsonResult(new { error = exception.Message }) { StatusCode = 400 }; }
            catch (InvalidOperationException exception) { return new JsonResult(new { error = exception.Message }) { StatusCode = 409 }; }
        }

        private static JsonResult BadRequestJson(string error) => new(new { error }) { StatusCode = 400 };
        private static JsonResult UnauthorizedJson() => new(new { error = "Phiên đăng nhập không hợp lệ." }) { StatusCode = 401 };

        private static QuestionBank ToQuestion(QuestionRequestBase request, Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(), CourseCode = request.CourseCode, QuestionText = request.QuestionText,
            OptionA = request.OptionA, OptionB = request.OptionB, OptionC = request.OptionC,
            OptionD = request.OptionD, CorrectAnswer = request.CorrectAnswer
        };

        public sealed class StartAttemptRequest { public Guid QuizId { get; set; } public string? Password { get; set; } }
        public sealed class QuizSubmitRequest { public Guid AttemptId { get; set; } public List<UserAnswerDto> Answers { get; set; } = new(); }
        public sealed class CreateQuizRequest
        {
            public string CourseCode { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public int QuestionCount { get; set; }
            public Guid? DocumentId { get; set; }
            public int MaxAttempts { get; set; } = 1;
            public int DurationMinutes { get; set; } = 30;
            public string? Password { get; set; }
            public QuizReviewPolicy ReviewPolicy { get; set; } = QuizReviewPolicy.ScoreOnly;
            public bool ShuffleQuestions { get; set; } = true;
            public bool ShuffleOptions { get; set; } = true;
        }
        public sealed class DeleteQuizRequest { public Guid Id { get; set; } public string CourseCode { get; set; } = string.Empty; }
        public abstract class QuestionRequestBase
        {
            public string CourseCode { get; set; } = string.Empty;
            public string QuestionText { get; set; } = string.Empty;
            public string OptionA { get; set; } = string.Empty;
            public string OptionB { get; set; } = string.Empty;
            public string OptionC { get; set; } = string.Empty;
            public string OptionD { get; set; } = string.Empty;
            public string CorrectAnswer { get; set; } = string.Empty;
        }
        public sealed class AddQuestionRequest : QuestionRequestBase { }
        public sealed class UpdateQuestionRequest : QuestionRequestBase { public Guid Id { get; set; } }
        public sealed class DeleteQuestionRequest { public Guid Id { get; set; } public string CourseCode { get; set; } = string.Empty; }
        public sealed class GenerateRequest { public Guid DocumentId { get; set; } }
    }
}
