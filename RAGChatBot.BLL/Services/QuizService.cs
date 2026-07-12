using Microsoft.Extensions.Logging;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public sealed class QuizService : IQuizService
    {
        private readonly IQuizRepository _quizRepository;
        private readonly IQuizGenerationService _quizGenerationService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<QuizService> _logger;

        public QuizService(
            IQuizRepository quizRepository,
            IQuizGenerationService quizGenerationService,
            IUserRepository userRepository,
            ILogger<QuizService> logger)
        {
            _quizRepository = quizRepository;
            _quizGenerationService = quizGenerationService;
            _userRepository = userRepository;
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

            // Chia nhỏ các chunks thành các khối block (tối đa ~5000 ký tự mỗi khối) để sinh được nhiều câu hỏi
            var blocks = new List<string>();
            var currentBlock = new System.Text.StringBuilder();
            foreach (var chunk in chunks)
            {
                if (currentBlock.Length + chunk.Length > 5000)
                {
                    blocks.Add(currentBlock.ToString());
                    currentBlock.Clear();
                }
                currentBlock.AppendLine(chunk);
            }
            if (currentBlock.Length > 0)
            {
                blocks.Add(currentBlock.ToString());
            }

            // Giới hạn tối đa 10 blocks để tránh quá nhiều request đến API (10 blocks * 10 câu = 100 câu)
            var selectedBlocks = blocks.Take(10).ToList();
            var generatedQuestionsList = new List<GeneratedQuizQuestion>();

            foreach (var blockText in selectedBlocks)
            {
                // Nếu chỉ có 1 block, gen 20 câu. Nếu có nhiều block, gen 10 câu mỗi block.
                int numQuestionsToGenerate = selectedBlocks.Count == 1 ? 20 : 10;
                var generated = await _quizGenerationService.GenerateQuestionsAsync(
                    BuildGenerationPrompt(blockText, numQuestionsToGenerate));
                if (generated != null && generated.Count > 0)
                {
                    generatedQuestionsList.AddRange(generated);
                }

                // Chờ ngắn giữa các request để giảm thiểu rate limit
                await Task.Delay(500);
            }

            if (generatedQuestionsList.Count == 0)
            {
                _logger.LogWarning("[QuizGenerator] AI không trả về câu hỏi hợp lệ.");
                return;
            }

            var questions = generatedQuestionsList
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

        public async Task<IReadOnlyList<QuestionBank>> GetQuestionBankByCourseAsync(string courseCode)
        {
            return await _quizRepository.GetQuestionsByCourseAsync(courseCode);
        }

        public async Task<IReadOnlyList<QuizAttemptDetailsDto>> GetAttemptsByCourseAsync(string courseCode)
        {
            var attempts = await _quizRepository.GetAttemptsByCourseAsync(courseCode);
            var users = await _userRepository.GetAllAsync();
            var userDict = users.ToDictionary(u => u.Id);

            return attempts.Select(a => new QuizAttemptDetailsDto
            {
                Id = a.Id,
                UserId = a.UserId,
                StudentName = userDict.TryGetValue(a.UserId, out var u) ? (string.IsNullOrEmpty(u.FullName) ? u.Username : u.FullName) : "N/A",
                StudentUsername = userDict.TryGetValue(a.UserId, out var usr) ? usr.Username : "N/A",
                CourseCode = a.CourseCode,
                Score = a.Score,
                TotalQuestions = a.TotalQuestions,
                Percentage = a.Percentage,
                AttemptedAt = a.AttemptedAt,
                QuizTitle = a.QuizTitle ?? "Luyện tập tự do"
            }).ToList();
        }

        public async Task<QuizAttemptResult> SubmitQuizAttemptAsync(
            Guid userId,
            string courseCode,
            Guid? quizId,
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

            string? quizTitle = null;
            if (quizId.HasValue && quizId.Value != Guid.Empty)
            {
                var quiz = await _quizRepository.GetQuizByIdAsync(quizId.Value);
                if (quiz != null)
                {
                    quizTitle = quiz.Title;
                }
            }

            await _quizRepository.AddAttemptAsync(new QuizAttempt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CourseCode = courseCode,
                Score = score,
                TotalQuestions = total,
                Percentage = percentage,
                AttemptedAt = attemptedAt,
                QuizId = quizId,
                QuizTitle = quizTitle
            });

            return new QuizAttemptResult
            {
                Score = score,
                TotalQuestions = total,
                Percentage = percentage,
                AttemptedAt = attemptedAt
            };
        }

        public async Task<QuestionBank> AddQuestionAsync(QuestionBank question)
        {
            if (question == null) throw new ArgumentNullException(nameof(question));
            var doc = await _quizRepository.GetFirstDocumentByCourseAsync(question.CourseCode);
            if (doc == null)
            {
                throw new InvalidOperationException("Môn học chưa có tài liệu nào. Vui lòng tải lên và duyệt ít nhất 1 tài liệu trước!");
            }
            question.DocumentId = doc.Id;
            await _quizRepository.AddQuestionAsync(question);
            return question;
        }

        public async Task<QuestionBank> UpdateQuestionAsync(QuestionBank question)
        {
            if (question == null) throw new ArgumentNullException(nameof(question));
            
            var existing = await _quizRepository.GetQuestionByIdAsync(question.Id);
            if (existing == null) throw new InvalidOperationException("Question not found.");

            existing.QuestionText = question.QuestionText;
            existing.OptionA = question.OptionA;
            existing.OptionB = question.OptionB;
            existing.OptionC = question.OptionC;
            existing.OptionD = question.OptionD;
            existing.CorrectAnswer = NormalizeCorrectAnswer(question.CorrectAnswer);

            await _quizRepository.UpdateQuestionAsync(existing);
            return existing;
        }

        public async Task DeleteQuestionAsync(Guid id)
        {
            await _quizRepository.DeleteQuestionAsync(id);
        }

        public async Task<IReadOnlyList<Quiz>> GetQuizzesByCourseAsync(string courseCode)
        {
            return await _quizRepository.GetQuizzesByCourseAsync(courseCode);
        }

        public async Task<Quiz> CreateQuizAsync(string courseCode, string title, int questionCount, Guid? documentId)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Tiêu đề không được để trống.", nameof(title));

            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                Title = title.Trim(),
                CourseCode = courseCode,
                QuestionCount = questionCount,
                DocumentId = documentId,
                CreatedAt = DateTime.UtcNow.AddHours(7)
            };

            await _quizRepository.AddQuizAsync(quiz);
            return quiz;
        }

        public async Task DeleteQuizAsync(Guid id)
        {
            await _quizRepository.DeleteQuizAsync(id);
        }

        public async Task<IReadOnlyList<QuizQuestionModel>> GetQuizQuestionsAsync(Guid quizId)
        {
            var quiz = await _quizRepository.GetQuizByIdAsync(quizId);
            if (quiz == null) throw new InvalidOperationException("Bài trắc nghiệm không tồn tại.");

            var questions = await _quizRepository.GetQuestionsByCourseAsync(quiz.CourseCode);

            if (quiz.DocumentId.HasValue && quiz.DocumentId.Value != Guid.Empty)
            {
                questions = questions.Where(q => q.DocumentId == quiz.DocumentId.Value).ToList();
            }

            var rng = new Random();
            return questions
                .OrderBy(q => rng.Next())
                .Take(quiz.QuestionCount)
                .Select(q => new QuizQuestionModel
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD
                })
                .ToList();
        }

        private static string BuildGenerationPrompt(string content, int count)
            => "Bạn là chuyên gia giáo dục. Hãy tạo đúng " + count + " câu hỏi trắc nghiệm bằng tiếng Việt " +
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
