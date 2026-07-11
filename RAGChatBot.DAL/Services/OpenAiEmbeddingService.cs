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

        public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
        {
            if (texts == null || texts.Count == 0)
            {
                return new List<float[]>();
            }

            var results = new List<float[]>();
            const int batchSize = 16; // Gom nhóm 16 chunks một lượt để an toàn cho Rate Limit
            
            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();
                try
                {
                    var baseUrlClean = _baseUrl.TrimEnd('/');
                    var requestUrl = $"{baseUrlClean}/embeddings";

                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    
                    var requestBody = new OpenAIEmbeddingRequest
                    {
                        Input = batch,
                        Model = _model
                    };

                    requestMessage.Content = JsonContent.Create(requestBody);

                    var response = await _httpClient.SendAsync(requestMessage);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Lỗi gọi API Batch Embedding: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                        
                        // Kích hoạt Mock Embedding cho batch bị lỗi này
                        foreach (var _ in batch)
                        {
                            results.Add(GenerateMockEmbedding());
                        }
                        continue;
                    }

                    var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
                    
                    if (result?.Data == null || result.Data.Length == 0)
                    {
                        _logger.LogWarning("Phản hồi Batch không chứa dữ liệu. Fallback Mock Embedding.");
                        foreach (var _ in batch)
                        {
                            results.Add(GenerateMockEmbedding());
                        }
                        continue;
                    }

                    // Sắp xếp theo Index trả về để đảm bảo trùng khớp thứ tự gốc của texts
                    var sortedData = result.Data.OrderBy(d => d.Index).ToList();
                    foreach (var item in sortedData)
                    {
                        var embedding = item.Embedding;
                        if (embedding.Length != 1536)
                        {
                            _logger.LogWarning("Kích thước Vector Embedding từ API ({ActualLength}) khác với kích thước yêu cầu (1536) trong Batch. Đang tự động điều chỉnh...", embedding.Length);
                            var adjusted = new float[1536];
                            Array.Copy(embedding, adjusted, Math.Min(embedding.Length, 1536));
                            results.Add(adjusted);
                        }
                        else
                        {
                            results.Add(embedding);
                        }
                    }

                    // Đảm bảo số lượng phần tử trả về đủ bằng cách bù đệm mock
                    while (results.Count < i + batch.Count)
                    {
                        results.Add(GenerateMockEmbedding());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình sinh batch embedding.");
                    foreach (var _ in batch)
                    {
                        results.Add(GenerateMockEmbedding());
                    }
                }

                // Chờ 600ms giữa các batch để tránh 429 Rate Limit (100 RPM)
                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(600);
                }
            }

            return results;
        }

        private float[] GenerateMockEmbedding()
        {
            var mockVector = new float[1536];
            var rand = new Random();
            for (int i = 0; i < 1536; i++)
            {
                mockVector[i] = (float)(rand.NextDouble() * 2 - 1) * 0.01f;
            }
            return mockVector;
        }

        // --- Các lớp mô hình dữ liệu nội bộ (DTOs) tương thích OpenAI API ---
        private class OpenAIEmbeddingRequest
        {
            [JsonPropertyName("input")]
            public object Input { get; set; } = string.Empty;

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
            [JsonPropertyName("index")]
            public int Index { get; set; }

            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}
