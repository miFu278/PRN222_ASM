using Microsoft.Extensions.Logging;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using System.Text;

namespace RAGChatBot.BLL.Services
{
    public sealed class QuizService : IQuizService
    {
        private readonly IQuizRepository _quizRepository;
        private readonly IQuizGenerationService _quizGenerationService;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IQuizEventService _quizEventService;
        private readonly ILogger<QuizService> _logger;

        public QuizService(
            IQuizRepository quizRepository,
            IQuizGenerationService quizGenerationService,
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IQuizEventService quizEventService,
            ILogger<QuizService> logger)
        {
            _quizRepository = quizRepository;
            _quizGenerationService = quizGenerationService;
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _quizEventService = quizEventService;
            _logger = logger;
        }

        public async Task GenerateQuizForDocumentAsync(Guid documentId)
        {
            var document = await _quizRepository.GetDocumentAsync(documentId);
            if (document is null) return;

            await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
            {
                Type = "QuestionBankGenerationStarted",
                CourseCode = document.CourseCode,
                EntityId = documentId,
                Status = "Processing"
            });

            try
            {
                var chunks = await _quizRepository.GetDocumentChunkContentsAsync(documentId);
                if (chunks.Count == 0) throw new InvalidOperationException("Tài liệu chưa có nội dung đã vector hóa.");

                var blocks = BuildBlocks(chunks, 5_000);
                var selectedBlocks = SelectEvenlyDistributed(blocks, 10);
                var generatedQuestions = new List<GeneratedQuizQuestion>();

                foreach (var block in selectedBlocks)
                {
                    var count = selectedBlocks.Count == 1 ? 20 : 10;
                    generatedQuestions.AddRange(await _quizGenerationService.GenerateQuestionsAsync(
                        BuildGenerationPrompt(block, count, document.Chapter)));
                }

                var questions = generatedQuestions
                    .Where(IsValidGeneratedQuestion)
                    .GroupBy(question => question.Question.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Select(question => new QuestionBank
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        CourseCode = document.CourseCode,
                        Chapter = document.Chapter,
                        QuestionText = question.Question.Trim(),
                        OptionA = question.OptionA.Trim(),
                        OptionB = question.OptionB.Trim(),
                        OptionC = question.OptionC.Trim(),
                        OptionD = question.OptionD.Trim(),
                        CorrectAnswer = NormalizeCorrectAnswer(question.CorrectAnswer),
                        CreatedAt = DateTime.UtcNow
                    })
                    .ToList();

                if (questions.Count == 0)
                    throw new InvalidOperationException("AI không trả về câu hỏi hợp lệ.");

                await _quizRepository.ReplaceQuestionsAsync(documentId, questions);
                await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
                {
                    Type = "QuestionBankChanged",
                    CourseCode = document.CourseCode,
                    EntityId = documentId,
                    Status = "Completed"
                });
            }
            catch
            {
                await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
                {
                    Type = "QuestionBankGenerationFailed",
                    CourseCode = document.CourseCode,
                    EntityId = documentId,
                    Status = "Failed"
                });
                throw;
            }
        }

        public async Task<IReadOnlyList<QuizQuestionModel>> GetQuizByCourseAsync(string courseCode)
            => (await _quizRepository.GetQuestionsByCourseAsync(courseCode)).Select(ToQuestionModel).ToList();

        public Task<IReadOnlyList<QuestionBank>> GetQuestionBankByCourseAsync(string courseCode)
            => _quizRepository.GetQuestionsByCourseAsync(courseCode);

        public async Task<IReadOnlyList<QuizAttemptDetailsDto>> GetAttemptsByCourseAsync(string courseCode)
            => await MapAttemptsAsync(await _quizRepository.GetAttemptsByCourseAsync(courseCode));

        public async Task<IReadOnlyList<QuizAttemptDetailsDto>> GetStudentAttemptsAsync(Guid userId, string? courseCode = null)
            => await MapAttemptsAsync(await _quizRepository.GetAttemptsByUserAsync(userId, courseCode));

        public async Task<QuizStartResult> StartQuizAttemptAsync(Guid userId, Guid quizId, string? password)
        {
            var quiz = await _quizRepository.GetQuizByIdAsync(quizId)
                ?? throw new KeyNotFoundException("Bài trắc nghiệm không tồn tại.");
            if (!quiz.IsPublished) throw new InvalidOperationException("Bài trắc nghiệm chưa được mở.");

            var existing = await _quizRepository.GetInProgressAttemptAsync(userId, quizId);
            if (existing is not null && existing.ExpiresAt > DateTime.UtcNow) return ToStartResult(existing);
            if (existing is not null)
            {
                existing.Status = QuizAttemptStatus.Submitted;
                existing.Score = 0;
                existing.Percentage = 0;
                existing.SubmittedAt = existing.ExpiresAt;
                existing.AttemptedAt = existing.ExpiresAt;
                await _quizRepository.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(quiz.PasswordHash) &&
                (string.IsNullOrEmpty(password) || !_passwordHasher.Verify(password, quiz.PasswordHash)))
            {
                throw new UnauthorizedAccessException("Mật khẩu bài trắc nghiệm không đúng.");
            }

            var usedAttempts = await _quizRepository.GetAttemptCountAsync(userId, quizId);
            if (quiz.MaxAttempts > 0 && usedAttempts >= quiz.MaxAttempts)
                throw new InvalidOperationException("Bạn đã sử dụng hết số lần làm bài.");

            var available = await _quizRepository.GetQuestionsByCourseAsync(quiz.CourseCode);
            if (quiz.DocumentId.HasValue)
                available = available.Where(question => question.DocumentId == quiz.DocumentId.Value).ToList();
            if (available.Count < quiz.QuestionCount)
                throw new InvalidOperationException($"Ngân hàng chỉ có {available.Count}/{quiz.QuestionCount} câu hỏi phù hợp.");

            var selected = quiz.ShuffleQuestions
                ? available.OrderBy(_ => Random.Shared.Next()).Take(quiz.QuestionCount).ToList()
                : available.Take(quiz.QuestionCount).ToList();

            var startedAt = DateTime.UtcNow;
            var attempt = new QuizAttempt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QuizId = quiz.Id,
                QuizTitle = quiz.Title,
                CourseCode = quiz.CourseCode,
                AttemptNumber = usedAttempts + 1,
                Status = QuizAttemptStatus.InProgress,
                TotalQuestions = selected.Count,
                ReviewPolicy = quiz.ReviewPolicy,
                StartedAt = startedAt,
                ExpiresAt = startedAt.AddMinutes(quiz.DurationMinutes),
                AttemptedAt = startedAt
            };

            for (var index = 0; index < selected.Count; index++)
                attempt.Answers.Add(CreateSnapshot(attempt.Id, selected[index], index + 1, quiz.ShuffleOptions));

            return ToStartResult(await _quizRepository.AddAttemptAsync(attempt));
        }

        public async Task<QuizAttemptResult> SubmitQuizAttemptAsync(
            Guid userId, Guid attemptId, IReadOnlyList<UserAnswerDto> answers)
        {
            var attempt = await _quizRepository.GetAttemptWithAnswersAsync(attemptId)
                ?? throw new KeyNotFoundException("Lượt làm bài không tồn tại.");
            if (attempt.UserId != userId) throw new UnauthorizedAccessException("Bạn không sở hữu lượt làm bài này.");
            if (attempt.Status != QuizAttemptStatus.InProgress) throw new InvalidOperationException("Lượt làm bài đã được nộp.");
            if (attempt.ExpiresAt <= DateTime.UtcNow)
            {
                attempt.Status = QuizAttemptStatus.Submitted;
                attempt.Score = 0;
                attempt.Percentage = 0;
                attempt.SubmittedAt = attempt.ExpiresAt;
                attempt.AttemptedAt = attempt.ExpiresAt;
                await _quizRepository.SaveChangesAsync();
                throw new InvalidOperationException("Thời gian làm bài đã hết.");
            }

            var submitted = answers
                .GroupBy(answer => answer.QuestionId)
                .ToDictionary(group => group.Key, group => NormalizeSelectedAnswer(group.Last().SelectedAnswer));

            foreach (var answer in attempt.Answers)
            {
                answer.SelectedAnswer = answer.QuestionId.HasValue && submitted.TryGetValue(answer.QuestionId.Value, out var selected)
                    ? selected
                    : null;
                answer.IsCorrect = !string.IsNullOrEmpty(answer.SelectedAnswer) &&
                    string.Equals(answer.SelectedAnswer, answer.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
            }

            attempt.Score = attempt.Answers.Count(answer => answer.IsCorrect == true);
            attempt.TotalQuestions = attempt.Answers.Count;
            attempt.Percentage = attempt.TotalQuestions == 0
                ? 0
                : Math.Round(attempt.Score * 100.0 / attempt.TotalQuestions, 2);
            attempt.Status = QuizAttemptStatus.Submitted;
            attempt.SubmittedAt = DateTime.UtcNow;
            attempt.AttemptedAt = attempt.SubmittedAt.Value;
            await _quizRepository.SaveChangesAsync();
            await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
            {
                Type = "AttemptSubmitted",
                CourseCode = attempt.CourseCode,
                EntityId = attempt.Id,
                Status = attempt.Status.ToString()
            });

            return new QuizAttemptResult
            {
                AttemptId = attempt.Id,
                Score = attempt.Score,
                TotalQuestions = attempt.TotalQuestions,
                Percentage = attempt.Percentage,
                AttemptedAt = attempt.SubmittedAt.Value
            };
        }

        public async Task<QuizReviewModel> GetAttemptReviewAsync(Guid requesterId, Guid attemptId, bool instructorView, string? managedCourseCode = null)
        {
            var attempt = await _quizRepository.GetAttemptWithAnswersAsync(attemptId)
                ?? throw new KeyNotFoundException("Lượt làm bài không tồn tại.");
            if (!instructorView && attempt.UserId != requesterId)
                throw new UnauthorizedAccessException("Bạn không sở hữu lượt làm bài này.");
            if (instructorView && !string.Equals(attempt.CourseCode, managedCourseCode, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Bạn không quản lý môn học của lượt làm bài này.");
            if (attempt.Status != QuizAttemptStatus.Submitted)
                throw new InvalidOperationException("Lượt làm bài chưa được nộp.");

            var showQuestions = instructorView || attempt.ReviewPolicy != QuizReviewPolicy.ScoreOnly;
            var showCorrect = instructorView || attempt.ReviewPolicy == QuizReviewPolicy.FullReview;
            return new QuizReviewModel
            {
                AttemptId = attempt.Id,
                QuizId = attempt.QuizId.GetValueOrDefault(),
                CourseCode = attempt.CourseCode,
                QuizTitle = attempt.QuizTitle ?? "Bài trắc nghiệm",
                AttemptNumber = attempt.AttemptNumber,
                Score = attempt.Score,
                TotalQuestions = attempt.TotalQuestions,
                Percentage = attempt.Percentage,
                SubmittedAt = attempt.SubmittedAt,
                ReviewPolicy = attempt.ReviewPolicy,
                Questions = showQuestions
                    ? attempt.Answers.OrderBy(answer => answer.DisplayOrder).Select(answer => new QuizReviewQuestionModel
                    {
                        DisplayOrder = answer.DisplayOrder,
                        QuestionText = answer.QuestionText,
                        OptionA = answer.OptionA,
                        OptionB = answer.OptionB,
                        OptionC = answer.OptionC,
                        OptionD = answer.OptionD,
                        SelectedAnswer = answer.SelectedAnswer,
                        CorrectAnswer = showCorrect ? answer.CorrectAnswer : null,
                        IsCorrect = answer.IsCorrect
                    }).ToList()
                    : Array.Empty<QuizReviewQuestionModel>()
            };
        }

        public async Task<QuestionBank> AddQuestionAsync(QuestionBank question)
        {
            var document = await _quizRepository.GetFirstDocumentByCourseAsync(question.CourseCode)
                ?? throw new InvalidOperationException("Môn học chưa có tài liệu.");
            question.Id = Guid.NewGuid();
            question.DocumentId = document.Id;
            question.Chapter = document.Chapter;
            question.CorrectAnswer = NormalizeCorrectAnswer(question.CorrectAnswer);
            question.CreatedAt = DateTime.UtcNow;
            await _quizRepository.AddQuestionAsync(question);
            await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
            {
                Type = "QuestionBankChanged",
                CourseCode = question.CourseCode,
                EntityId = question.Id,
                Status = "Created"
            });
            return question;
        }

        public async Task<QuestionBank> UpdateQuestionAsync(QuestionBank question)
        {
            var existing = await _quizRepository.GetQuestionByIdAsync(question.Id)
                ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");
            if (!string.Equals(existing.CourseCode, question.CourseCode, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Câu hỏi không thuộc môn học bạn đang quản lý.");
            existing.QuestionText = RequireText(question.QuestionText, "Nội dung câu hỏi");
            existing.OptionA = RequireText(question.OptionA, "Đáp án A");
            existing.OptionB = RequireText(question.OptionB, "Đáp án B");
            existing.OptionC = RequireText(question.OptionC, "Đáp án C");
            existing.OptionD = RequireText(question.OptionD, "Đáp án D");
            existing.CorrectAnswer = NormalizeCorrectAnswer(question.CorrectAnswer);
            await _quizRepository.UpdateQuestionAsync(existing);
            await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
            {
                Type = "QuestionBankChanged",
                CourseCode = existing.CourseCode,
                EntityId = existing.Id,
                Status = "Updated"
            });
            return existing;
        }

        public async Task DeleteQuestionAsync(Guid id, string courseCode)
        {
            var question = await _quizRepository.GetQuestionByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy câu hỏi.");
            if (!string.Equals(question.CourseCode, courseCode, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Câu hỏi không thuộc môn học bạn đang quản lý.");
            await _quizRepository.DeleteQuestionAsync(id);
            await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
            {
                Type = "QuestionBankChanged",
                CourseCode = question.CourseCode,
                EntityId = question.Id,
                Status = "Deleted"
            });
        }

        public async Task<IReadOnlyList<QuizSummaryModel>> GetQuizzesByCourseAsync(
            string courseCode, Guid? userId = null, bool includeUnpublished = false)
        {
            var quizzes = await _quizRepository.GetQuizzesByCourseAsync(courseCode);
            var result = new List<QuizSummaryModel>();
            foreach (var quiz in quizzes.Where(quiz => includeUnpublished || quiz.IsPublished))
            {
                result.Add(new QuizSummaryModel
                {
                    Id = quiz.Id,
                    Title = quiz.Title,
                    CourseCode = quiz.CourseCode,
                    QuestionCount = quiz.QuestionCount,
                    DocumentId = quiz.DocumentId,
                    MaxAttempts = quiz.MaxAttempts,
                    DurationMinutes = quiz.DurationMinutes,
                    AttemptsUsed = userId.HasValue ? await _quizRepository.GetAttemptCountAsync(userId.Value, quiz.Id) : 0,
                    HasPassword = !string.IsNullOrWhiteSpace(quiz.PasswordHash),
                    ReviewPolicy = quiz.ReviewPolicy,
                    ShuffleQuestions = quiz.ShuffleQuestions,
                    ShuffleOptions = quiz.ShuffleOptions,
                    IsPublished = quiz.IsPublished,
                    CreatedAt = quiz.CreatedAt
                });
            }
            return result;
        }

        public async Task<Quiz> CreateQuizAsync(
            string courseCode, string title, int questionCount, Guid? documentId,
            int maxAttempts, int durationMinutes, string? password, QuizReviewPolicy reviewPolicy,
            bool shuffleQuestions, bool shuffleOptions)
        {
            if (questionCount is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(questionCount));
            if (maxAttempts is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (durationMinutes is < 1 or > 300) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            var normalizedCourseCode = RequireText(courseCode, "Mã môn").ToUpperInvariant();
            var available = await _quizRepository.GetQuestionsByCourseAsync(normalizedCourseCode);
            if (documentId.HasValue)
            {
                var document = await _quizRepository.GetDocumentAsync(documentId.Value)
                    ?? throw new KeyNotFoundException("Không tìm thấy tài liệu đã chọn.");
                if (!string.Equals(document.CourseCode, normalizedCourseCode, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Tài liệu không thuộc môn học này.");
                available = available.Where(question => question.DocumentId == documentId.Value).ToList();
            }
            if (available.Count < questionCount)
                throw new InvalidOperationException($"Ngân hàng chỉ có {available.Count}/{questionCount} câu hỏi phù hợp.");
            var quiz = new Quiz
            {
                Id = Guid.NewGuid(),
                Title = RequireText(title, "Tiêu đề"),
                CourseCode = normalizedCourseCode,
                QuestionCount = questionCount,
                DocumentId = documentId,
                MaxAttempts = maxAttempts,
                DurationMinutes = durationMinutes,
                PasswordHash = string.IsNullOrWhiteSpace(password) ? null : _passwordHasher.Hash(password),
                ReviewPolicy = reviewPolicy,
                ShuffleQuestions = shuffleQuestions,
                ShuffleOptions = shuffleOptions,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            };
            await _quizRepository.AddQuizAsync(quiz);
            await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
            {
                Type = "QuizCatalogChanged",
                CourseCode = quiz.CourseCode,
                EntityId = quiz.Id,
                Status = "Created"
            });
            return quiz;
        }

        public async Task DeleteQuizAsync(Guid id, string courseCode)
        {
            var quiz = await _quizRepository.GetQuizByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy bài trắc nghiệm.");
            if (!string.Equals(quiz.CourseCode, courseCode, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Bài trắc nghiệm không thuộc môn học bạn đang quản lý.");
            await _quizRepository.DeleteQuizAsync(id);
            await _quizEventService.NotifyQuizChangedAsync(new RealtimeChangeEvent
            {
                Type = "QuizCatalogChanged",
                CourseCode = quiz.CourseCode,
                EntityId = quiz.Id,
                Status = "Deleted"
            });
        }

        private async Task<IReadOnlyList<QuizAttemptDetailsDto>> MapAttemptsAsync(IReadOnlyList<QuizAttempt> attempts)
        {
            var users = (await _userRepository.GetAllAsync()).ToDictionary(user => user.Id);
            return attempts.Select(attempt => new QuizAttemptDetailsDto
            {
                Id = attempt.Id,
                UserId = attempt.UserId,
                StudentName = users.TryGetValue(attempt.UserId, out var user)
                    ? (string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName)
                    : "N/A",
                StudentUsername = users.TryGetValue(attempt.UserId, out var account) ? account.Username : "N/A",
                CourseCode = attempt.CourseCode,
                QuizId = attempt.QuizId.GetValueOrDefault(),
                QuizTitle = attempt.QuizTitle,
                AttemptNumber = attempt.AttemptNumber,
                Status = attempt.Status.ToString(),
                Score = attempt.Score,
                TotalQuestions = attempt.TotalQuestions,
                Percentage = attempt.Percentage,
                AttemptedAt = attempt.AttemptedAt,
                SubmittedAt = attempt.SubmittedAt
            }).ToList();
        }

        private static QuizStartResult ToStartResult(QuizAttempt attempt) => new()
        {
            AttemptId = attempt.Id,
            AttemptNumber = attempt.AttemptNumber,
            ExpiresAt = attempt.ExpiresAt,
            Questions = attempt.Answers.OrderBy(answer => answer.DisplayOrder).Select(answer => new QuizQuestionModel
            {
                Id = answer.QuestionId ?? answer.Id,
                QuestionText = answer.QuestionText,
                OptionA = answer.OptionA,
                OptionB = answer.OptionB,
                OptionC = answer.OptionC,
                OptionD = answer.OptionD
            }).ToList()
        };

        private static QuizAttemptAnswer CreateSnapshot(Guid attemptId, QuestionBank question, int order, bool shuffleOptions)
        {
            var options = new List<(string Original, string Text)>
            {
                ("A", question.OptionA), ("B", question.OptionB), ("C", question.OptionC), ("D", question.OptionD)
            };
            if (shuffleOptions) options = options.OrderBy(_ => Random.Shared.Next()).ToList();
            var displayedLetters = new[] { "A", "B", "C", "D" };
            var correctIndex = options.FindIndex(option => option.Original == NormalizeCorrectAnswer(question.CorrectAnswer));
            return new QuizAttemptAnswer
            {
                Id = Guid.NewGuid(),
                AttemptId = attemptId,
                QuestionId = question.Id,
                DisplayOrder = order,
                QuestionText = question.QuestionText,
                OptionA = options[0].Text,
                OptionB = options[1].Text,
                OptionC = options[2].Text,
                OptionD = options[3].Text,
                CorrectAnswer = displayedLetters[correctIndex]
            };
        }

        private static QuizQuestionModel ToQuestionModel(QuestionBank question) => new()
        {
            Id = question.Id,
            QuestionText = question.QuestionText,
            OptionA = question.OptionA,
            OptionB = question.OptionB,
            OptionC = question.OptionC,
            OptionD = question.OptionD
        };

        private static List<string> BuildBlocks(IReadOnlyList<string> chunks, int maxLength)
        {
            var blocks = new List<string>();
            var current = new StringBuilder();
            foreach (var chunk in chunks)
            {
                if (current.Length > 0 && current.Length + chunk.Length > maxLength)
                {
                    blocks.Add(current.ToString());
                    current.Clear();
                }
                current.AppendLine(chunk);
            }
            if (current.Length > 0) blocks.Add(current.ToString());
            return blocks;
        }

        private static List<string> SelectEvenlyDistributed(IReadOnlyList<string> blocks, int maximum)
        {
            if (blocks.Count <= maximum) return blocks.ToList();
            return Enumerable.Range(0, maximum)
                .Select(index => blocks[(int)Math.Round(index * (blocks.Count - 1d) / (maximum - 1d))])
                .ToList();
        }

        private static bool IsValidGeneratedQuestion(GeneratedQuizQuestion question)
            => !string.IsNullOrWhiteSpace(question.Question) &&
               !string.IsNullOrWhiteSpace(question.OptionA) && !string.IsNullOrWhiteSpace(question.OptionB) &&
               !string.IsNullOrWhiteSpace(question.OptionC) && !string.IsNullOrWhiteSpace(question.OptionD) &&
               NormalizeCorrectAnswer(question.CorrectAnswer) is "A" or "B" or "C" or "D";

        private static string BuildGenerationPrompt(string content, int count, string chapter)
            => $"Tạo đúng {count} câu hỏi trắc nghiệm tiếng Việt cho chương '{chapter}'. " +
               "Chỉ dùng thông tin trong nội dung nguồn, không suy đoán. Trả về JSON array với question, a, b, c, d, correct; " +
               $"correct chỉ là A/B/C/D. Không markdown.\n\n[NỘI DUNG NGUỒN]\n{content}\n\nJSON:";

        private static string NormalizeCorrectAnswer(string? answer)
        {
            var normalized = answer?.Trim().ToUpperInvariant();
            return normalized is "A" or "B" or "C" or "D" ? normalized : string.Empty;
        }

        private static string? NormalizeSelectedAnswer(string? answer)
        {
            var normalized = answer?.Trim().ToUpperInvariant();
            return normalized is "A" or "B" or "C" or "D" ? normalized : null;
        }

        private static string RequireText(string? value, string field)
            => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{field} không được để trống.") : value.Trim();
    }
}
