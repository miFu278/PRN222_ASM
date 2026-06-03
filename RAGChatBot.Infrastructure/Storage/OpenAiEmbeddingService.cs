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
                    throw new Exception($"Lỗi kết nối AI Embedding Gateway ({response.StatusCode}): {errorContent}");
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
                
                if (result?.Data == null || result.Data.Length == 0 || result.Data[0].Embedding == null)
                {
                    throw new Exception("Phản hồi từ AI Gateway không chứa dữ liệu Embedding hợp lệ.");
                }

                return result.Data[0].Embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi sinh Vector Embedding cho văn bản");
                throw;
            }
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
