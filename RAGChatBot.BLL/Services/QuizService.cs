using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RAGChatBot.DAL.Context;
using RAGChatBot.DAL.Entities;
using RAGChatBot.DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public class QuizService : IQuizService
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _httpClient;
        private readonly ILogger<QuizService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        public QuizService(
            AppDbContext db,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<QuizService> logger)
        {
            _db = db;
            _httpClient = httpClient;
            _logger = logger;

            var section = configuration.GetSection("AiSettings");
            _baseUrl = section["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/openai";
            _apiKey = section["ApiKey"] ?? string.Empty;
            _model = section["ChatModel"] ?? "gemini-2.0-flash";
        }

        public async Task GenerateQuizForDocumentAsync(Guid documentId)
        {
            _logger.LogInformation("[QuizGenerator] Bắt đầu tự động sinh Quiz cho tài liệu ID={DocId}", documentId);

            var document = await _db.KnowledgeDocuments.FindAsync(documentId);
            if (document == null)
            {
                _logger.LogWarning("[QuizGenerator] Không tìm thấy tài liệu ID={DocId}", documentId);
                return;
            }

            var chunks = await _db.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .Select(c => c.Content)
                .ToListAsync();

            if (!chunks.Any())
            {
                _logger.LogWarning("[QuizGenerator] Tài liệu ID={DocId} không có phân mảnh văn bản nào để sinh câu hỏi", documentId);
                return;
            }

            // Kết hợp nội dung các chunks tài liệu để gửi lên LLM
            string combinedText = string.Join("\n", chunks);
            if (combinedText.Length > 8000)
            {
                combinedText = combinedText.Substring(0, 8000); // Giới hạn ngữ cảnh gửi lên AI
            }

            var prompt = "Bạn là một chuyên gia học thuật giáo dục. Hãy đọc nội dung tài liệu dưới đây và tự động sinh ra bộ 5 câu hỏi trắc nghiệm ôn tập (Multiple-Choice Questions) bằng tiếng Việt dựa vào nội dung tài liệu này.\n\n" +
                         $"[NỘI DUNG TÀI LIỆU]:\n{combinedText}\n\n" +
                         "Yêu cầu định dạng kết quả trả về:\n" +
                         "- BẮT BUỘC trả về chuỗi định dạng JSON hợp lệ là một mảng các đối tượng chứa các trường sau:\n" +
                         "  - \"question\": câu hỏi trắc nghiệm rõ ràng, cụ thể dựa trên kiến thức tài liệu\n" +
                         "  - \"a\": Lựa chọn A\n" +
                         "  - \"b\": Lựa chọn B\n" +
                         "  - \"c\": Lựa chọn C\n" +
                         "  - \"d\": Lựa chọn D\n" +
                         "  - \"correct\": đáp án đúng chỉ ghi chữ cái in hoa tương ứng (\"A\", \"B\", \"C\", \"D\")\n" +
                         "- Chỉ trả về duy nhất chuỗi JSON array, không bao bọc bởi thẻ markdown ```json, không thêm lời chào hay giải thích gì thêm.\n\n" +
                         "JSON:";

            try
            {
                var baseUrlClean = _baseUrl.TrimEnd('/');
                var requestUrl = $"{baseUrlClean}/chat/completions";

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                var requestBody = new OpenAiChatRequest
                {
                    Model = _model,
                    Messages = new List<OpenAiChatMessage>
                    {
                        new OpenAiChatMessage { Role = "user", Content = prompt }
                    },
                    Temperature = 0.5f
                };

                requestMessage.Content = JsonContent.Create(requestBody);
                var response = await _httpClient.SendAsync(requestMessage);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[QuizGenerator] Lỗi gọi API chat/completions: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>();
                var reply = result?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrWhiteSpace(reply))
                {
                    _logger.LogWarning("[QuizGenerator] LLM trả về kết quả rỗng.");
                    return;
                }

                // Clean markdown block
                var cleanJson = reply.Trim();
                if (cleanJson.StartsWith("```"))
                {
                    int firstNewLine = cleanJson.IndexOf('\n');
                    int lastBackticks = cleanJson.LastIndexOf("```");
                    if (firstNewLine != -1 && lastBackticks != -1 && lastBackticks > firstNewLine)
                    {
                        cleanJson = cleanJson.Substring(firstNewLine + 1, lastBackticks - firstNewLine - 1).Trim();
                    }
                }

                // Phân tích cú pháp JSON
                var questions = JsonSerializer.Deserialize<List<LlmQuizQuestion>>(cleanJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (questions == null || !questions.Any())
                {
                    _logger.LogWarning("[QuizGenerator] Không thể phân tích kết quả JSON từ LLM.");
                    return;
                }

                // Xóa các câu hỏi cũ của tài liệu này (nếu có) trước khi tạo mới
                var oldQuestions = await _db.QuestionBanks.Where(q => q.DocumentId == documentId).ToListAsync();
                if (oldQuestions.Any())
                {
                    _db.QuestionBanks.RemoveRange(oldQuestions);
                }

                foreach (var q in questions)
                {
                    var correctLetter = q.Correct?.ToUpper().Trim() ?? "A";
                    if (correctLetter != "A" && correctLetter != "B" && correctLetter != "C" && correctLetter != "D")
                    {
                        correctLetter = "A"; // Default safe fallback
                    }

                    var entity = new QuestionBank
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        CourseCode = document.CourseCode,
                        QuestionText = q.Question ?? "Câu hỏi trắc nghiệm tài liệu",
                        OptionA = q.A ?? "Đáp án A",
                        OptionB = q.B ?? "Đáp án B",
                        OptionC = q.C ?? "Đáp án C",
                        OptionD = q.D ?? "Đáp án D",
                        CorrectAnswer = correctLetter,
                        CreatedAt = DateTime.UtcNow.AddHours(7)
                    };

                    _db.QuestionBanks.Add(entity);
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("[QuizGenerator] Đã tự động sinh thành công {Count} câu hỏi trắc nghiệm cho tài liệu ID={DocId}", questions.Count, documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QuizGenerator] Lỗi trong quá trình sinh câu hỏi trắc nghiệm.");
            }
        }

        public async Task<IEnumerable<QuestionBank>> GetQuizByCourseAsync(string courseCode)
        {
            return await _db.QuestionBanks
                .Where(q => q.CourseCode.ToLower() == courseCode.ToLower())
                .OrderBy(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<QuizAttempt> SubmitQuizAttemptAsync(Guid userId, string courseCode, List<UserAnswerDto> answers)
        {
            var questionIds = answers.Select(a => a.QuestionId).ToList();
            var questions = await _db.QuestionBanks
                .Where(q => questionIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            int score = 0;
            int total = questions.Count;

            foreach (var ans in answers)
            {
                if (questions.TryGetValue(ans.QuestionId, out var q))
                {
                    if (string.Equals(q.CorrectAnswer, ans.SelectedAnswer?.ToUpper().Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        score++;
                    }
                }
            }

            double percentage = total > 0 ? (score * 100.0) / total : 0;

            var attempt = new QuizAttempt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CourseCode = courseCode,
                Score = score,
                TotalQuestions = total,
                Percentage = Math.Round(percentage, 2),
                AttemptedAt = DateTime.UtcNow.AddHours(7)
            };

            _db.QuizAttempts.Add(attempt);
            await _db.SaveChangesAsync();

            return attempt;
        }

        // --- Model dữ liệu gọi LLM ---
        private class OpenAiChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<OpenAiChatMessage> Messages { get; set; } = new();

            [JsonPropertyName("temperature")]
            public float Temperature { get; set; } = 0.5f;
        }

        private class OpenAiChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class OpenAiChatResponse
        {
            [JsonPropertyName("choices")]
            public List<OpenAiChatChoice> Choices { get; set; } = new();
        }

        private class OpenAiChatChoice
        {
            [JsonPropertyName("message")]
            public OpenAiChatMessage Message { get; set; } = new();
        }

        private class LlmQuizQuestion
        {
            [JsonPropertyName("question")]
            public string Question { get; set; } = string.Empty;

            [JsonPropertyName("a")]
            public string A { get; set; } = string.Empty;

            [JsonPropertyName("b")]
            public string B { get; set; } = string.Empty;

            [JsonPropertyName("c")]
            public string C { get; set; } = string.Empty;

            [JsonPropertyName("d")]
            public string D { get; set; } = string.Empty;

            [JsonPropertyName("correct")]
            public string Correct { get; set; } = string.Empty;
        }
    }
}
