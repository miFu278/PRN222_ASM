using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class PaymentServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPaymentTransactionRepository _transactions = Substitute.For<IPaymentTransactionRepository>();

    [Fact]
    public async Task CreatePending_UsesProvidedOrderId_AndPersistsPendingTransaction()
    {
        var user = new User { Id = Guid.NewGuid() };
        _users.GetByIdAsync(user.Id).Returns(user);
        PaymentTransaction? added = null;
        _transactions.AddAsync(Arg.Do<PaymentTransaction>(item => added = item)).Returns(Task.CompletedTask);

        var orderId = await CreateService().CreatePendingTransactionAsync(user.Id, 99000, "ORDER-1");

        Assert.Equal("ORDER-1", orderId);
        Assert.NotNull(added);
        Assert.Equal(user.Id, added.UserId);
        Assert.Equal(99000, added.Amount);
        Assert.Equal(PaymentTransactionTypes.PremiumSubscription, added.Type);
        Assert.Equal("Pending", added.Status);
        await _transactions.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task CreatePending_RejectsMissingUser_WithoutWriting()
    {
        _users.GetByIdAsync(Arg.Any<Guid>()).Returns((User?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().CreatePendingTransactionAsync(Guid.NewGuid(), 99000));

        await _transactions.DidNotReceive().AddAsync(Arg.Any<PaymentTransaction>());
    }

    [Fact]
    public async Task InvalidCallback_IsRejectedWithoutChangingTransaction()
    {
        var result = await CreateService().ProcessPaymentCallbackAsync(new PayOSCallbackResult
        {
            IsValid = false,
            OrderId = "ORDER-1"
        }, Guid.NewGuid());

        Assert.False(result);
        await _transactions.DidNotReceive().CompletePaymentAsync(
            Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<Guid?>());
        await _transactions.DidNotReceive().MarkFailedAsync(Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task FailedCallback_IsMarkedFailedForExpectedUser()
    {
        var userId = Guid.NewGuid();

        var result = await CreateService().ProcessPaymentCallbackAsync(new PayOSCallbackResult
        {
            IsValid = true,
            IsSuccess = false,
            OrderId = "ORDER-2"
        }, userId);

        Assert.False(result);
        await _transactions.Received(1).MarkFailedAsync("ORDER-2", userId);
    }

    [Fact]
    public async Task SuccessfulCallback_CompletesOnlyForExpectedUserAndAmount()
    {
        var userId = Guid.NewGuid();
        _transactions.CompletePaymentAsync("ORDER-3", 99000, "TX-1", userId).Returns(true);

        var result = await CreateService().ProcessPaymentCallbackAsync(new PayOSCallbackResult
        {
            IsValid = true,
            IsSuccess = true,
            OrderId = "ORDER-3",
            Amount = 99000,
            TransactionNo = "TX-1"
        }, userId);

        Assert.True(result);
        await _transactions.Received(1).CompletePaymentAsync("ORDER-3", 99000, "TX-1", userId);
    }

    [Fact]
    public async Task VerifiedWebhook_CompletesWithoutBrowserUserConstraint()
    {
        _transactions.CompletePaymentAsync("ORDER-4", 99000, "TX-2", null).Returns(true);

        var result = await CreateService().ProcessVerifiedPaymentAsync("ORDER-4", 99000, "TX-2");

        Assert.True(result);
        await _transactions.Received(1).CompletePaymentAsync("ORDER-4", 99000, "TX-2", null);
    }

    [Fact]
    public async Task GetTransactionsByUser_ReturnsRepositoryResultsForThatUser()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FullName = "Nguyen Van A",
            Username = "student-a"
        };
        _transactions.GetByUserIdAsync(userId).Returns(new List<PaymentTransaction>
        {
            new()
            {
                UserId = userId,
                User = user,
                OrderId = "ORDER-5",
                Amount = 199000,
                Type = PaymentTransactionTypes.PremiumSubscription,
                Status = "Success"
            }
        });

        var result = (await CreateService().GetTransactionsByUserAsync(userId)).ToList();

        var transaction = Assert.Single(result);
        Assert.Equal("ORDER-5", transaction.OrderId);
        Assert.Equal("student-a", transaction.Username);
        Assert.Equal(PaymentTransactionTypes.PremiumSubscription, transaction.Type);
        await _transactions.Received(1).GetByUserIdAsync(userId);
        await _transactions.DidNotReceive().GetAllAsync();
    }

    [Fact]
    public async Task GetAllTransactions_ForwardsStatusAndTypeFiltersToRepository()
    {
        _transactions.GetAllAsync("Success", PaymentTransactionTypes.PremiumSubscription)
            .Returns(Array.Empty<PaymentTransaction>());

        var result = await CreateService().GetAllTransactionsAsync(
            "Success", PaymentTransactionTypes.PremiumSubscription);

        Assert.Empty(result);
        await _transactions.Received(1).GetAllAsync(
            "Success", PaymentTransactionTypes.PremiumSubscription);
    }

    private PaymentService CreateService() => new(_users, _transactions);
}
