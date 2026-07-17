using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RAGChatBot.DAL.Services;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class AiAdapterTests
{
    [Fact]
    public async Task QuizGenerator_ParsesMarkdownFencedJsonAndSendsConfiguredModel()
    {
        var generated = """
            [{"question":"Q?","a":"A1","b":"B1","c":"C1","d":"D1","correct":"B"}]
            """;
        var response = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content = $"```json\n{generated}\n```" } } }
        });
        var handler = StubHttpMessageHandler.Json(response);
        var service = new OpenAiQuizGenerationService(
            new HttpClient(handler),
            Configuration(),
            NullLogger<OpenAiQuizGenerationService>.Instance);

        var result = await service.GenerateQuestionsAsync("Generate one question");

        var question = Assert.Single(result);
        Assert.Equal("Q?", question.Question);
        Assert.Equal("B", question.CorrectAnswer);
        Assert.Contains("gemini-test", handler.LastRequestBody);
        Assert.Contains("Generate one question", handler.LastRequestBody);
    }

    [Fact]
    public async Task QuizGenerator_ServiceUnavailableReturnsEmptyList()
    {
        var handler = StubHttpMessageHandler.Json("{\"error\":\"busy\"}", HttpStatusCode.ServiceUnavailable);
        var service = new OpenAiQuizGenerationService(
            new HttpClient(handler),
            Configuration(),
            NullLogger<OpenAiQuizGenerationService>.Instance);

        var result = await service.GenerateQuestionsAsync("Generate");

        Assert.Empty(result);
    }

    [Fact]
    public async Task QuizGenerator_MalformedResponseReturnsEmptyList()
    {
        var response = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content = "not-json" } } }
        });
        var service = new OpenAiQuizGenerationService(
            new HttpClient(StubHttpMessageHandler.Json(response)),
            Configuration(),
            NullLogger<OpenAiQuizGenerationService>.Instance);

        Assert.Empty(await service.GenerateQuestionsAsync("Generate"));
    }

    [Fact]
    public async Task Embedding_BlankTextReturnsEmptyWithoutHttpCall()
    {
        var handler = StubHttpMessageHandler.Json("{}");
        var service = new OpenAiEmbeddingService(
            new HttpClient(handler),
            Configuration(),
            NullLogger<OpenAiEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingAsync("  ");

        Assert.Empty(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Embedding_ValidResponseReturns1536DimensionsAndPrefixesQuery()
    {
        var vector = Enumerable.Repeat(0.25f, 1536).ToArray();
        var handler = StubHttpMessageHandler.Json(JsonSerializer.Serialize(new
        {
            data = new[] { new { index = 0, embedding = vector } }
        }));
        var service = new OpenAiEmbeddingService(
            new HttpClient(handler),
            Configuration(),
            NullLogger<OpenAiEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingAsync("  dependency injection  ");

        Assert.Equal(1536, result.Length);
        Assert.Contains("task: search result | query: dependency injection", handler.LastRequestBody);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task Embedding_WrongDimensionThrows()
    {
        var handler = StubHttpMessageHandler.Json(JsonSerializer.Serialize(new
        {
            data = new[] { new { index = 0, embedding = new[] { 0.1f, 0.2f } } }
        }));
        var service = new OpenAiEmbeddingService(
            new HttpClient(handler),
            Configuration(),
            NullLogger<OpenAiEmbeddingService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateEmbeddingAsync("query"));
    }

    [Fact]
    public async Task BatchEmbedding_SortsVectorsByReturnedIndex()
    {
        var first = Enumerable.Repeat(1f, 1536).ToArray();
        var second = Enumerable.Repeat(2f, 1536).ToArray();
        var handler = StubHttpMessageHandler.Json(JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { index = 1, embedding = second },
                new { index = 0, embedding = first }
            }
        }));
        var service = new OpenAiEmbeddingService(
            new HttpClient(handler),
            Configuration(),
            NullLogger<OpenAiEmbeddingService>.Instance);

        var result = await service.GenerateEmbeddingsAsync(new List<string> { "first", "second" });

        Assert.Equal(2, result.Count);
        Assert.Equal(1f, result[0][0]);
        Assert.Equal(2f, result[1][0]);
    }

    [Fact]
    public async Task RagChat_NoRelevantChunksReturnsGroundedMessageWithoutCallingChatApi()
    {
        var embeddings = Substitute.For<IEmbeddingService>();
        var documents = Substitute.For<IKnowledgeDocumentRepository>();
        embeddings.GenerateEmbeddingAsync(Arg.Any<string>()).Returns(new[] { 1f });
        documents.SearchSimilarChunksAsync(
                "PRN222", Arg.Any<float[]>(), 8, Arg.Any<double>())
            .Returns(Array.Empty<RelevantDocumentChunk>());
        var handler = StubHttpMessageHandler.Json("{}");
        var service = new OpenAiChatService(
            new HttpClient(handler),
            embeddings,
            documents,
            Configuration(),
            NullLogger<OpenAiChatService>.Instance);

        var result = await service.GetChatResponseAsync(
            "Explain middleware", "PRN222", Array.Empty<ChatHistoryItem>());

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Sources);
        Assert.Contains("Không tìm thấy nội dung", result.Reply);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RagChat_UsesRecentHistoryAndReturnsRetrievedSources()
    {
        var embeddings = Substitute.For<IEmbeddingService>();
        var documents = Substitute.For<IKnowledgeDocumentRepository>();
        embeddings.GenerateEmbeddingAsync(Arg.Any<string>()).Returns(new[] { 1f });
        var source = new RelevantDocumentChunk
        {
            DocumentId = Guid.NewGuid(),
            FileName = "middleware.pdf",
            CourseCode = "PRN222",
            ChunkIndex = 2,
            Content = "Middleware forms an HTTP pipeline.",
            Distance = 0.1
        };
        documents.SearchSimilarChunksAsync(
                "PRN222", Arg.Any<float[]>(), 8, Arg.Any<double>())
            .Returns(new[] { source });
        var response = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { role = "assistant", content = "Answer [Nguồn 1]" } } }
        });
        var handler = StubHttpMessageHandler.Json(response);
        var service = new OpenAiChatService(
            new HttpClient(handler),
            embeddings,
            documents,
            Configuration(),
            NullLogger<OpenAiChatService>.Instance);
        var history = new[]
        {
            new ChatHistoryItem("user", "Previous question"),
            new ChatHistoryItem("assistant", "Previous answer"),
            new ChatHistoryItem("system", "must be ignored")
        };

        var result = await service.GetChatResponseAsync(
            "Explain pipeline", "PRN222", history);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Answer [Nguồn 1]", result.Reply);
        Assert.Equal(source.DocumentId, Assert.Single(result.Sources).DocumentId);
        Assert.Contains("Previous question", handler.LastRequestBody);
        Assert.DoesNotContain("must be ignored", handler.LastRequestBody);
        await embeddings.Received(1).GenerateEmbeddingAsync(
            Arg.Is<string>(query => query != null &&
                query.Contains("Previous question") &&
                query.EndsWith("Explain pipeline")));
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiSettings:BaseUrl"] = "https://ai.test/v1",
            ["AiSettings:ApiKey"] = "test-key",
            ["AiSettings:ChatModel"] = "gemini-test",
            ["AiSettings:EmbeddingModel"] = "embedding-test",
            ["RagSettings:MaxCosineDistance"] = "0.42"
        })
        .Build();
}
