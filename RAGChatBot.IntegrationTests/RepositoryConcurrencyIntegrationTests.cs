using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.IntegrationTests;

[Collection(E2ECollection.Name)]
public sealed class RepositoryConcurrencyIntegrationTests(E2ETestFixture fixture)
{
    [Fact]
    public async Task DailyQuota_IsAtomicUnderParallelRequests_AndNeverExceedsLimit()
    {
        var user = await fixture.AddUserAsync(
            $"quota-{Guid.NewGuid():N}", "E2E-password", SystemRoleIds.Student);
        var usageDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>();
            return await repository.TryConsumeDailyCreditAsync(user.Id, "PRN222", usageDate, 10);
        }));

        Assert.Equal(10, results.Count(result => result.Allowed));
        Assert.Equal(15, results.Count(result => !result.Allowed));

        await using var verificationScope = fixture.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sessions = await db.ChatSessions
            .Where(session => session.UserId == user.Id && session.UsageDate == usageDate)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(sessions);
        Assert.Equal(10, sessions[0].MessageCount);
    }

    [Fact]
    public async Task ParallelPaymentCallbacks_AreIdempotent_AndExtendSubscriptionOnlyOnce()
    {
        var user = await fixture.AddUserAsync(
            $"payment-{Guid.NewGuid():N}", "E2E-password", SystemRoleIds.Student);
        var orderId = $"ORDER-{Guid.NewGuid():N}";
        const long amount = 99000;

        await using (var setupScope = fixture.Services.CreateAsyncScope())
        {
            var repository = setupScope.ServiceProvider.GetRequiredService<IPaymentTransactionRepository>();
            await repository.AddAsync(new PaymentTransaction
            {
                Id = Guid.NewGuid(), OrderId = orderId, UserId = user.Id, Amount = amount, Status = "Pending"
            });
            await repository.SaveChangesAsync();
        }

        var before = DateTime.UtcNow.AddMonths(1);
        var callbacks = await Task.WhenAll(Enumerable.Range(0, 5).Select(async index =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPaymentTransactionRepository>();
            return await repository.CompletePaymentAsync(orderId, amount, $"TX-{index}", user.Id);
        }));
        var after = DateTime.UtcNow.AddMonths(1);

        Assert.All(callbacks, Assert.True);
        await using var verificationScope = fixture.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedUser = await db.Users.SingleAsync(item => item.Id == user.Id, TestContext.Current.CancellationToken);
        var storedTransaction = await db.PaymentTransactions.SingleAsync(
            item => item.OrderId == orderId, TestContext.Current.CancellationToken);
        Assert.Equal("Success", storedTransaction.Status);
        Assert.Equal("Premium", storedUser.SubscriptionTier);
        Assert.NotNull(storedUser.SubscriptionExpiresAt);
        Assert.InRange(storedUser.SubscriptionExpiresAt.Value, before, after);
    }

    [Fact]
    public async Task ParallelQuizStarts_ReturnTheSameSingleActiveAttempt()
    {
        var user = await fixture.AddUserAsync(
            $"quiz-{Guid.NewGuid():N}", "E2E-password", SystemRoleIds.Student);
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(), Title = "Concurrency quiz", CourseCode = "PRN222",
            QuestionCount = 1, DurationMinutes = 30, MaxAttempts = 3
        };
        await using (var setupScope = fixture.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Quizzes.Add(quiz);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var returnedAttempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IQuizRepository>();
            var now = DateTime.UtcNow;
            return await repository.AddAttemptAsync(new QuizAttempt
            {
                Id = Guid.NewGuid(), UserId = user.Id, QuizId = quiz.Id, CourseCode = quiz.CourseCode,
                QuizTitle = quiz.Title, AttemptNumber = 1, Status = QuizAttemptStatus.InProgress,
                StartedAt = now, ExpiresAt = now.AddMinutes(30), AttemptedAt = now
            });
        }));

        Assert.Single(returnedAttempts.Select(attempt => attempt.Id).Distinct());
        await using var verificationScope = fixture.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verificationDb.QuizAttempts.CountAsync(
            attempt => attempt.UserId == user.Id && attempt.QuizId == quiz.Id &&
                attempt.Status == QuizAttemptStatus.InProgress,
            TestContext.Current.CancellationToken));
    }
}
