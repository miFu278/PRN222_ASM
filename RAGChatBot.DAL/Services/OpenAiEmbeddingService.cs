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
        private const int EmbeddingDimensions = 1536;

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
                    Input = $"task: search result | query: {text.Trim()}",
                    Model = _model
                };

                requestMessage.Content = JsonContent.Create(requestBody);

                var response = await _httpClient.SendAsync(requestMessage);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException(
                        $"Embedding API returned {(int)response.StatusCode}: {errorContent}");
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
                
                if (result?.Data == null || result.Data.Length == 0 || result.Data[0].Embedding == null)
                {
                    throw new InvalidOperationException("Embedding API did not return an embedding vector.");
                }

                var embedding = result.Data[0].Embedding;
                if (embedding.Length != EmbeddingDimensions)
                {
                    throw new InvalidOperationException(
                        $"Embedding API returned {embedding.Length} dimensions instead of {EmbeddingDimensions}.");
                }

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể sinh vector embedding cho câu truy vấn.");
                throw;
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
                var batch = texts
                    .Skip(i)
                    .Take(batchSize)
                    .Select(text => $"title: none | text: {text.Trim()}")
                    .ToList();
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
                        throw new HttpRequestException(
                            $"Batch embedding API returned {(int)response.StatusCode}: {errorContent}");
                    }

                    var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
                    
                    if (result?.Data == null || result.Data.Length == 0)
                    {
                        throw new InvalidOperationException("Batch embedding API did not return vectors.");
                    }

                    // Sắp xếp theo Index trả về để đảm bảo trùng khớp thứ tự gốc của texts
                    var sortedData = result.Data.OrderBy(d => d.Index).ToList();
                    foreach (var item in sortedData)
                    {
                        var embedding = item.Embedding;
                        if (embedding.Length != EmbeddingDimensions)
                        {
                            throw new InvalidOperationException(
                                $"Batch embedding API returned {embedding.Length} dimensions instead of {EmbeddingDimensions}.");
                        }
                        results.Add(embedding);
                    }

                    if (sortedData.Count != batch.Count)
                    {
                        throw new InvalidOperationException(
                            $"Batch embedding API returned {sortedData.Count} vectors for {batch.Count} inputs.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình sinh batch embedding.");
                    throw;
                }

                // Chờ 600ms giữa các batch để tránh 429 Rate Limit (100 RPM)
                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(600);
                }
            }

            return results;
        }

        // --- Các lớp mô hình dữ liệu nội bộ (DTOs) tương thích OpenAI API ---
        private class OpenAIEmbeddingRequest
        {
            [JsonPropertyName("input")]
            public object Input { get; set; } = string.Empty;

            [JsonPropertyName("model")]
            public string Model { get; set; } = "text-embedding-3-small";

            [JsonPropertyName("dimensions")]
            public int Dimensions { get; set; } = EmbeddingDimensions;
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
