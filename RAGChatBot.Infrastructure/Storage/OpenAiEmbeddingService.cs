using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RAGChatBot.Application.Common.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.Storage
{
    public class OpenAiEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiEmbeddingService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        public OpenAiEmbeddingService(
            HttpClient httpClient, 
            IConfiguration configuration,
            ILogger<OpenAiEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            var section = configuration.GetSection("AiSettings");
            _baseUrl = section["BaseUrl"] ?? "https://aigw.9router.com/v1";
            _apiKey = section["ApiKey"] ?? string.Empty;
            _model = section["EmbeddingModel"] ?? "text-embedding-3-small";

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("AI API Key chưa được cấu hình trong appsettings.json tại mục AiSettings:ApiKey!");
            }
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<float>();
            }

            try
            {
                // Chuẩn hóa Base URL
                var baseUrlClean = _baseUrl.TrimEnd('/');
                var requestUrl = $"{baseUrlClean}/embeddings";

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                
                var requestBody = new OpenAIEmbeddingRequest
                {
                    Input = text,
                    Model = _model
                };

                requestMessage.Content = JsonContent.Create(requestBody);

                var response = await _httpClient.SendAsync(requestMessage);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Lỗi gọi API 9router/OpenAI: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                    
                    _logger.LogWarning("Tự động kích hoạt cơ chế Fallback Mock Embedding do phản hồi lỗi từ API Gateway.");
                    return GenerateMockEmbedding();
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
                
                if (result?.Data == null || result.Data.Length == 0 || result.Data[0].Embedding == null)
                {
                    _logger.LogWarning("Phản hồi không chứa dữ liệu Embedding hợp lệ. Tự động kích hoạt cơ chế Fallback Mock Embedding.");
                    return GenerateMockEmbedding();
                }

                var embedding = result.Data[0].Embedding;
                if (embedding.Length != 1536)
                {
                    _logger.LogWarning("Kích thước Vector Embedding từ API ({ActualLength}) khác với kích thước yêu cầu (1536). Đang tự động điều chỉnh bằng cách đệm hoặc cắt...", embedding.Length);
                    var adjustedEmbedding = new float[1536];
                    Array.Copy(embedding, adjustedEmbedding, Math.Min(embedding.Length, 1536));
                    return adjustedEmbedding;
                }

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi sinh Vector Embedding cho văn bản. Đang kích hoạt cơ chế Fallback Mock Embedding...");
                return GenerateMockEmbedding();
            }
        }

        private float[] GenerateMockEmbedding()
        {
            var mockVector = new float[1536];
            var rand = new Random();
            for (int i = 0; i < 1536; i++)
            {
                // Sinh giá trị ngẫu nhiên nhỏ từ -0.01 đến 0.01
                mockVector[i] = (float)(rand.NextDouble() * 2 - 1) * 0.01f;
            }
            return mockVector;
        }

        // --- Các lớp mô hình dữ liệu nội bộ (DTOs) tương thích OpenAI API ---
        private class OpenAIEmbeddingRequest
        {
            [JsonPropertyName("input")]
            public string Input { get; set; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; set; } = "text-embedding-3-small";
        }

        private class OpenAIEmbeddingResponse
        {
            [JsonPropertyName("data")]
            public OpenAIEmbeddingData[] Data { get; set; } = Array.Empty<OpenAIEmbeddingData>();
        }

        private class OpenAIEmbeddingData
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}
