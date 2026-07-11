using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RAGChatBot.DAL.Services
{
    public sealed class OpenAiQuizGenerationService : IQuizGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiQuizGenerationService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _model;

        public OpenAiQuizGenerationService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenAiQuizGenerationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            var section = configuration.GetSection("AiSettings");
            _baseUrl = section["BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/openai";
            _apiKey = section["ApiKey"] ?? string.Empty;
            _model = section["ChatModel"] ?? "gemini-2.0-flash";
        }

        public async Task<IReadOnlyList<GeneratedQuizQuestion>> GenerateQuestionsAsync(string prompt)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_baseUrl.TrimEnd('/')}/chat/completions");

                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                request.Content = JsonContent.Create(new OpenAiChatRequest
                {
                    Model = _model,
                    Messages = new List<OpenAiChatMessage>
                    {
                        new() { Role = "user", Content = prompt }
                    },
                    Temperature = 0.5f
                });

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError(
                        "[QuizGenerator] AI request failed: {StatusCode} - {Error}",
                        response.StatusCode,
                        error);
                    return Array.Empty<GeneratedQuizQuestion>();
                }

                var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>();
                var content = result?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Array.Empty<GeneratedQuizQuestion>();
                }

                var cleanJson = RemoveMarkdownFence(content);
                var questions = JsonSerializer.Deserialize<List<LlmQuizQuestion>>(
                    cleanJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (questions is null)
                {
                    return Array.Empty<GeneratedQuizQuestion>();
                }

                return questions
                    .Select(question => new GeneratedQuizQuestion
                    {
                        Question = question.Question ?? string.Empty,
                        OptionA = question.A ?? string.Empty,
                        OptionB = question.B ?? string.Empty,
                        OptionC = question.C ?? string.Empty,
                        OptionD = question.D ?? string.Empty,
                        CorrectAnswer = question.Correct ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QuizGenerator] Failed to parse AI response.");
                return Array.Empty<GeneratedQuizQuestion>();
            }
        }

        private static string RemoveMarkdownFence(string content)
        {
            var cleanJson = content.Trim();
            if (!cleanJson.StartsWith("```", StringComparison.Ordinal))
            {
                return cleanJson;
            }

            var firstNewLine = cleanJson.IndexOf('\n');
            var lastBackticks = cleanJson.LastIndexOf("```", StringComparison.Ordinal);
            return firstNewLine >= 0 && lastBackticks > firstNewLine
                ? cleanJson[(firstNewLine + 1)..lastBackticks].Trim()
                : cleanJson;
        }

        private sealed class OpenAiChatRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; init; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<OpenAiChatMessage> Messages { get; init; } = new();

            [JsonPropertyName("temperature")]
            public float Temperature { get; init; }
        }

        private sealed class OpenAiChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; init; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; init; } = string.Empty;
        }

        private sealed class OpenAiChatResponse
        {
            [JsonPropertyName("choices")]
            public List<OpenAiChatChoice> Choices { get; init; } = new();
        }

        private sealed class OpenAiChatChoice
        {
            [JsonPropertyName("message")]
            public OpenAiChatMessage Message { get; init; } = new();
        }

        private sealed class LlmQuizQuestion
        {
            [JsonPropertyName("question")]
            public string? Question { get; init; }

            [JsonPropertyName("a")]
            public string? A { get; init; }

            [JsonPropertyName("b")]
            public string? B { get; init; }

            [JsonPropertyName("c")]
            public string? C { get; init; }

            [JsonPropertyName("d")]
            public string? D { get; init; }

            [JsonPropertyName("correct")]
            public string? Correct { get; init; }
        }
    }
}
