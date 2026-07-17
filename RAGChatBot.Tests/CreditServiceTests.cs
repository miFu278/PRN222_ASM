using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class CreditServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IChatSessionRepository _sessions = Substitute.For<IChatSessionRepository>();

    [Fact]
    public async Task ActivePremiumUser_UsesFiftyDailyCredits()
    {
        var userId = Guid.NewGuid();
        _users.GetByIdAsync(userId).Returns(new User
        {
            Id = userId,
            SubscriptionTier = "Premium",
            SubscriptionExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        _sessions.TryConsumeDailyCreditAsync(userId, "PRN222", Arg.Any<DateOnly>(), 50)
            .Returns((true, 49));

        var result = await CreateService().CheckAndDeductCreditAsync(userId, "PRN222");

        Assert.Equal((true, 49), result);
        await _sessions.Received(1).TryConsumeDailyCreditAsync(
            userId, "PRN222", Arg.Any<DateOnly>(), 50);
    }

    [Theory]
    [InlineData("Free", 30)]
    [InlineData("Premium", -1)]
    public async Task NonPremiumOrExpiredUser_UsesAtomicDailyQuota(string tier, int expiryOffsetDays)
    {
        var userId = Guid.NewGuid();
        _users.GetByIdAsync(userId).Returns(new User
        {
            Id = userId,
            SubscriptionTier = tier,
            SubscriptionExpiresAt = DateTime.UtcNow.AddDays(expiryOffsetDays)
        });
        _sessions.TryConsumeDailyCreditAsync(userId, "PRN222", Arg.Any<DateOnly>(), 10)
            .Returns((true, 7));

        var result = await CreateService().CheckAndDeductCreditAsync(userId, "PRN222");

        Assert.Equal((true, 7), result);
        await _sessions.Received(1).TryConsumeDailyCreditAsync(
            userId, "PRN222", Arg.Any<DateOnly>(), 10);
    }

    [Fact]
    public async Task Refund_UsesCurrentVietnamDate()
    {
        var userId = Guid.NewGuid();
        DateOnly? capturedDate = null;
        _sessions.RefundDailyCreditAsync(userId, Arg.Do<DateOnly>(date => capturedDate = date))
            .Returns(Task.CompletedTask);

        await CreateService().RefundCreditAsync(userId);

        Assert.NotNull(capturedDate);
        Assert.InRange(capturedDate.Value, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
    }

    private CreditService CreateService() => new(_users, _sessions);
}
