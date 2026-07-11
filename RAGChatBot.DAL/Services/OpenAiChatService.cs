using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RAGChatBot.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RAGChatBot.DAL.Services
{
    public class OpenAiChatService : IChatResponseService
    {
        private readonly HttpClient _httpClient;
        private readonly IEmbeddingService _embeddingService;
        private readonly IKnowledgeDocumentRepository _documentRepository;
        private readonly ILogger<OpenAiChatService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        public OpenAiChatService(
            HttpClient httpClient,
            IEmbeddingService embeddingService,
            IKnowledgeDocumentRepository documentRepository,
            IConfiguration configuration,
            ILogger<OpenAiChatService> _loggerVal)
        {
            _httpClient = httpClient;
            _embeddingService = embeddingService;
            _documentRepository = documentRepository;
            _logger = _loggerVal;

            var section = configuration.GetSection("AiSettings");
            _baseUrl = section["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/openai";
            _apiKey = section["ApiKey"] ?? string.Empty;
            _model = section["ChatModel"] ?? "gemini-2.0-flash";

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("AI API Key cho Chatbot chưa được cấu hình tại mục AiSettings:ApiKey!");
            }
        }
        public async Task<string> GetChatResponseAsync(
            string question,
            string? courseCode,
            IReadOnlyList<ChatHistoryItem> history)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return "Vui lòng nhập câu hỏi của bạn.";
            }

            try
            {
                // 1. Sinh vector nhúng cho câu hỏi
                var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);
                if (questionEmbedding == null || questionEmbedding.Length == 0)
                {
                    return "Không thể sinh Vector Embedding cho câu hỏi này. Vui lòng kiểm tra lại dịch vụ AI.";
                }
                
                // 2. Tìm kiếm các đoạn tài liệu liên quan nhất
                var similarChunks = await _documentRepository.SearchSimilarChunksAsync(courseCode, questionEmbedding, topK: 5);
                var chunksList = similarChunks.ToList();

                // 3. Xây dựng ngữ cảnh (context) từ các mảnh tài liệu
                string contextText = "";
                if (chunksList.Count > 0)
                {
                    contextText = string.Join("\n\n", chunksList.Select((c, idx) => $"[Đoạn tài liệu {idx + 1} từ tệp {c.Document.FileName} (Mã môn: {c.Document.CourseCode})]:\n{c.Content}"));
                }
                else
                {
                    contextText = "Không có tài liệu môn học nào được phê duyệt hoặc phù hợp với câu hỏi này.";
                }

                // 4. Tạo prompt hệ thống với cấu trúc RAG
                string courseIntro = string.IsNullOrEmpty(courseCode) ? "toàn bộ các môn học" : $"môn học có mã {courseCode}";
                var systemPrompt = $"Bạn là một trợ lý giảng dạy AI hữu ích cho {courseIntro}. Hãy sử dụng ngữ cảnh dưới đây từ các tài liệu môn học đã được phê duyệt để trả lời câu hỏi của sinh viên. Trả lời một cách khoa học, rõ ràng và đầy đủ bằng tiếng Việt.\n\n" +
                                   $"[NGỮ CẢNH TỪ TÀI LIỆU MÔN HỌC]:\n{contextText}\n\n" +
                                   $"[LƯU Ý QUAN TRỌNG]:\n" +
                                   $"- Nếu bạn lấy thông tin từ ngữ cảnh, LUÔN LUÔN trích dẫn NGUỒN TÀI LIỆU (Ví dụ: \"Theo tài liệu: tên_file.pdf\") ngay trong câu trả lời hoặc liệt kê ở cuối câu trả lời.\n" +
                                   $"- Nếu ngữ cảnh không chứa thông tin cần thiết để trả lời câu hỏi, hãy tự trả lời dựa trên kiến thức phổ thông/chuyên môn của bạn nhưng bắt buộc phải ghi rõ câu mở đầu: \"Lưu ý: Không tìm thấy thông tin này trực tiếp trong tài liệu môn học. Dưới đây là câu trả lời dựa trên kiến thức chung:\".";

                // 5. Gọi API hoàn thiện hội thoại (Chat Completion API)
                var baseUrlClean = _baseUrl.TrimEnd('/');
                var requestUrl = $"{baseUrlClean}/chat/completions";

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                // Xây dựng list tin nhắn gồm System context, Lịch sử chat (Multi-turn), và câu hỏi hiện tại
                var messages = new List<OpenAiChatMessage>
                {
                    new OpenAiChatMessage { Role = "system", Content = systemPrompt }
                };

                foreach (var message in history)
                {
                    if (message.Role is "user" or "assistant")
                    {
                        messages.Add(new OpenAiChatMessage
                        {
                            Role = message.Role,
                            Content = message.Content
                        });
                    }
                }

                messages.Add(new OpenAiChatMessage { Role = "user", Content = question });

                var requestBody = new OpenAiChatRequest
                {
                    Model = _model,
                    Messages = messages,
                    Temperature = 0.7f
                };

                requestMessage.Content = JsonContent.Create(requestBody);
                var response = await _httpClient.SendAsync(requestMessage);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Lỗi gọi API chat/completions: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                    return "Xin lỗi, đã xảy ra lỗi khi kết nối tới dịch vụ AI. Vui lòng thử lại sau.";
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>();
                var reply = result?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrWhiteSpace(reply))
                {
                    return "Không thể nhận phản hồi từ AI. Vui lòng thử lại.";
                }

                return reply;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý Chatbot RAG cho môn {CourseCode}", courseCode);
                return $"Đã xảy ra lỗi ngoài ý muốn khi xử lý câu hỏi của bạn: {ex.Message}";
            }
        }

        // --- Các lớp mô hình dữ liệu nội bộ (DTOs) tương thích OpenAI Chat Completion API ---
        private class OpenAiChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<OpenAiChatMessage> Messages { get; set; } = new();

            [JsonPropertyName("temperature")]
            public float Temperature { get; set; } = 0.7f;
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
    }
}
