using Microsoft.Extensions.Logging;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;

namespace RAGChatBot.BLL.Services
{
    public sealed class QuizService : IQuizService
    {
        private readonly IQuizRepository _quizRepository;
        private readonly IQuizGenerationService _quizGenerationService;
        private readonly ILogger<QuizService> _logger;

        public QuizService(
            IQuizRepository quizRepository,
            IQuizGenerationService quizGenerationService,
            ILogger<QuizService> logger)
        {
            _quizRepository = quizRepository;
            _quizGenerationService = quizGenerationService;
            _logger = logger;
        }

        public async Task GenerateQuizForDocumentAsync(Guid documentId)
        {
            _logger.LogInformation(
                "[QuizGenerator] Bắt đầu sinh quiz cho tài liệu ID={DocumentId}",
                documentId);

            var document = await _quizRepository.GetDocumentAsync(documentId);
            if (document is null)
            {
                _logger.LogWarning(
                    "[QuizGenerator] Không tìm thấy tài liệu ID={DocumentId}",
                    documentId);
                return;
            }

            var chunks = await _quizRepository.GetDocumentChunkContentsAsync(documentId);
            if (chunks.Count == 0)
            {
                _logger.LogWarning(
                    "[QuizGenerator] Tài liệu ID={DocumentId} không có nội dung để sinh câu hỏi",
                    documentId);
                return;
            }

            var combinedText = string.Join("\n", chunks);
            if (combinedText.Length > 8000)
            {
                combinedText = combinedText[..8000];
            }

            var generatedQuestions = await _quizGenerationService.GenerateQuestionsAsync(
                BuildGenerationPrompt(combinedText));
            if (generatedQuestions.Count == 0)
            {
                _logger.LogWarning("[QuizGenerator] AI không trả về câu hỏi hợp lệ.");
                return;
            }

            var questions = generatedQuestions
                .Select(question => new QuestionBank
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    CourseCode = document.CourseCode,
                    QuestionText = DefaultIfBlank(question.Question, "Câu hỏi trắc nghiệm tài liệu"),
                    OptionA = DefaultIfBlank(question.OptionA, "Đáp án A"),
                    OptionB = DefaultIfBlank(question.OptionB, "Đáp án B"),
                    OptionC = DefaultIfBlank(question.OptionC, "Đáp án C"),
                    OptionD = DefaultIfBlank(question.OptionD, "Đáp án D"),
                    CorrectAnswer = NormalizeCorrectAnswer(question.CorrectAnswer),
                    CreatedAt = DateTime.UtcNow.AddHours(7)
                })
                .ToList();

            await _quizRepository.ReplaceQuestionsAsync(documentId, questions);
            _logger.LogInformation(
                "[QuizGenerator] Đã sinh {Count} câu hỏi cho tài liệu ID={DocumentId}",
                questions.Count,
                documentId);
        }

        public async Task<IReadOnlyList<QuizQuestionModel>> GetQuizByCourseAsync(string courseCode)
        {
            var questions = await _quizRepository.GetQuestionsByCourseAsync(courseCode);
            return questions
                .Select(question => new QuizQuestionModel
                {
                    Id = question.Id,
                    QuestionText = question.QuestionText,
                    OptionA = question.OptionA,
                    OptionB = question.OptionB,
                    OptionC = question.OptionC,
                    OptionD = question.OptionD
                })
                .ToList();
        }

        public async Task<QuizAttemptResult> SubmitQuizAttemptAsync(
            Guid userId,
            string courseCode,
            IReadOnlyList<UserAnswerDto> answers)
        {
            var questionIds = answers.Select(answer => answer.QuestionId).Distinct().ToList();
            var questions = await _quizRepository.GetQuestionsByIdsAsync(questionIds);

            var score = answers.Count(answer =>
                questions.TryGetValue(answer.QuestionId, out var question) &&
                string.Equals(
                    question.CorrectAnswer,
                    answer.SelectedAnswer.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            var total = questions.Count;
            var percentage = total > 0 ? Math.Round(score * 100.0 / total, 2) : 0;
            var attemptedAt = DateTime.UtcNow.AddHours(7);

            await _quizRepository.AddAttemptAsync(new QuizAttempt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CourseCode = courseCode,
                Score = score,
                TotalQuestions = total,
                Percentage = percentage,
                AttemptedAt = attemptedAt
            });

            return new QuizAttemptResult
            {
                Score = score,
                TotalQuestions = total,
                Percentage = percentage,
                AttemptedAt = attemptedAt
            };
        }

        private static string BuildGenerationPrompt(string content)
            => "Bạn là chuyên gia giáo dục. Hãy tạo 5 câu hỏi trắc nghiệm bằng tiếng Việt " +
               "dựa trên nội dung tài liệu dưới đây. Chỉ trả về một JSON array hợp lệ; " +
               "mỗi phần tử có các trường question, a, b, c, d và correct. " +
               "Trường correct chỉ nhận A, B, C hoặc D. Không dùng markdown fence.\n\n" +
               $"[NỘI DUNG TÀI LIỆU]:\n{content}\n\nJSON:";

        private static string NormalizeCorrectAnswer(string answer)
        {
            var normalized = answer.Trim().ToUpperInvariant();
            return normalized is "A" or "B" or "C" or "D" ? normalized : "A";
        }

        private static string DefaultIfBlank(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
