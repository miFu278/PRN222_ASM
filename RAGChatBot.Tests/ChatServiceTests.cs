using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class ChatServiceTests
{
    private readonly IChatRepository _chats = Substitute.For<IChatRepository>();
    private readonly IChatResponseService _responses = Substitute.For<IChatResponseService>();
    private readonly IChatTrackerLogRepository _logs = Substitute.For<IChatTrackerLogRepository>();
    private readonly ICreditService _credits = Substitute.For<ICreditService>();
    private readonly ICourseRepository _courses = Substitute.For<ICourseRepository>();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankQuestion_IsRejectedBeforeAnyDependencyCall(string question)
    {
        var result = await CreateService().SendMessageAsync(Guid.NewGuid(), question, "PRN222", null);

        Assert.NotNull(result);
        Assert.True(result.IsError);
        await _courses.DidNotReceive().GetByCodeAsync(Arg.Any<string>());
        await _credits.DidNotReceive().CheckAndDeductCreditAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExistingThreadThatDoesNotBelongToUser_ReturnsNull()
    {
        _chats.GetThreadForUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((ChatThread?)null);

        var result = await CreateService().SendMessageAsync(
            Guid.NewGuid(), "Question", "PRN222", Guid.NewGuid());

        Assert.Null(result);
        await _credits.DidNotReceive().CheckAndDeductCreditAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task MissingCourse_IsRejectedBeforeCreditReservation()
    {
        _courses.GetByCodeAsync("PRN222").Returns((Course?)null);

        var result = await CreateService().SendMessageAsync(
            Guid.NewGuid(), "Question", " prn222 ", null);

        Assert.NotNull(result);
        Assert.True(result.IsError);
        await _credits.DidNotReceive().CheckAndDeductCreditAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExhaustedQuota_DoesNotCallAiOrPersistChat()
    {
        var userId = Guid.NewGuid();
        _courses.GetByCodeAsync("PRN222").Returns(new Course { Code = "PRN222" });
        _credits.CheckAndDeductCreditAsync(userId, "PRN222").Returns((false, 0));

        var result = await CreateService().SendMessageAsync(userId, "Question", "PRN222", null);

        Assert.NotNull(result);
        Assert.True(result.OutOfCredits);
        await _responses.DidNotReceive().GetChatResponseAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatHistoryItem>>());
        await _chats.DidNotReceive().AddExchangeAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task AiFailure_RefundsReservedCredit_AndDoesNotCreateThread()
    {
        var userId = Guid.NewGuid();
        _courses.GetByCodeAsync("PRN222").Returns(new Course { Code = "PRN222" });
        _credits.CheckAndDeductCreditAsync(userId, "PRN222").Returns((true, 5));
        _responses.GetChatResponseAsync("Question", "PRN222", Arg.Any<IReadOnlyList<ChatHistoryItem>>())
            .Returns(new ChatResponseResult("AI unavailable", false, Array.Empty<ChatSource>()));

        var result = await CreateService().SendMessageAsync(userId, "Question", "PRN222", null);

        Assert.NotNull(result);
        Assert.True(result.IsError);
        Assert.Equal(6, result.Remaining);
        await _credits.Received(1).RefundCreditAsync(userId);
        await _chats.DidNotReceive().CreateThreadAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task SuccessfulResponse_CreatesThread_PersistsExchange_AndClampsSourceRelevance()
    {
        var userId = Guid.NewGuid();
        var thread = new ChatThread { Id = Guid.NewGuid(), UserId = userId, CourseCode = "PRN222" };
        _courses.GetByCodeAsync("PRN222").Returns(new Course { Code = "PRN222" });
        _credits.CheckAndDeductCreditAsync(userId, "PRN222").Returns((true, 4));
        _responses.GetChatResponseAsync("Question", "PRN222", Arg.Any<IReadOnlyList<ChatHistoryItem>>())
            .Returns(new ChatResponseResult("Answer", true, new[]
            {
                new ChatSource(Guid.NewGuid(), "source.pdf", "PRN222", 1, -0.2, "Preview content"),
                new ChatSource(Guid.NewGuid(), "source.pdf", "PRN222", 2, 2.0)
            }));
        _chats.CreateThreadAsync(userId, "PRN222", "Question", Arg.Any<DateTime>()).Returns(thread);

        var result = await CreateService().SendMessageAsync(userId, " Question ", "prn222", null);

        Assert.NotNull(result);
        Assert.False(result.IsError);
        Assert.Equal(thread.Id, result.ThreadId);
        Assert.Equal(new[] { 1d, 0d }, result.Sources.Select(source => source.Relevance));
        Assert.Equal("Preview content", result.Sources[0].Content);
        await _chats.Received(1).AddExchangeAsync(
            thread.Id, "Question", "Answer", Arg.Any<DateTime>());
        await _logs.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task AuditFailure_DoesNotFailSuccessfulChatResponse()
    {
        var userId = Guid.NewGuid();
        var thread = new ChatThread { Id = Guid.NewGuid(), UserId = userId, CourseCode = "PRN222" };
        _courses.GetByCodeAsync("PRN222").Returns(new Course { Code = "PRN222" });
        _credits.CheckAndDeductCreditAsync(userId, "PRN222").Returns((true, 9));
        _responses.GetChatResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatHistoryItem>>())
            .Returns(new ChatResponseResult("Answer", true, Array.Empty<ChatSource>()));
        _chats.CreateThreadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>()).Returns(thread);
        _logs.AddAsync(Arg.Any<ChatTrackerLog>()).Returns<Task>(_ => throw new InvalidOperationException("log failed"));

        var result = await CreateService().SendMessageAsync(userId, "Question", "PRN222", null);

        Assert.NotNull(result);
        Assert.False(result.IsError);
        Assert.Equal("Answer", result.Reply);
    }

    private ChatService CreateService() => new(
        _chats, _responses, _logs, _credits, _courses, NullLogger<ChatService>.Instance);
}
