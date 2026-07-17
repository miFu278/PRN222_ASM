using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class AppDbContextModelTests
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(
            "Host=localhost;Database=model_only;Username=test;Password=test",
            options => options.UseVector())
        .Options);

    [Fact]
    public void ChatSession_HasUniqueDailyQuotaIndex()
    {
        var entity = _db.Model.FindEntityType(typeof(ChatSession));
        var index = entity!.GetIndexes().Single(item =>
            item.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(ChatSession.UserId), nameof(ChatSession.UsageDate) }));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void PaymentTransaction_OrderIdIsUnique()
    {
        var entity = _db.Model.FindEntityType(typeof(PaymentTransaction));
        var index = entity!.GetIndexes().Single(item =>
            item.Properties.Count == 1 && item.Properties[0].Name == nameof(PaymentTransaction.OrderId));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void PaymentTransaction_TypeIsRequiredIndexedAndDefaultsToPremiumSubscription()
    {
        var entity = _db.Model.FindEntityType(typeof(PaymentTransaction));
        var type = entity!.FindProperty(nameof(PaymentTransaction.Type));
        var index = entity.GetIndexes().Single(item =>
            item.Properties.Count == 1 && item.Properties[0].Name == nameof(PaymentTransaction.Type));

        Assert.NotNull(type);
        Assert.False(type.IsNullable);
        Assert.Equal(50, type.GetMaxLength());
        Assert.Equal(PaymentTransactionTypes.PremiumSubscription, type.GetDefaultValue());
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void QuizAttempt_UsesPostgresXminForOptimisticConcurrency()
    {
        var entity = _db.Model.FindEntityType(typeof(QuizAttempt));
        var version = entity!.FindProperty(nameof(QuizAttempt.Version));

        Assert.NotNull(version);
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
    }

    [Fact]
    public void QuizAttempt_AllowsOnlyOneInProgressAttemptPerUserAndQuiz()
    {
        var entity = _db.Model.FindEntityType(typeof(QuizAttempt));
        var index = entity!.GetIndexes().Single(item =>
            item.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(QuizAttempt.UserId), nameof(QuizAttempt.QuizId) }));

        Assert.True(index.IsUnique);
        Assert.Contains("Status", index.GetFilter());
        Assert.Contains("QuizId", index.GetFilter());
    }
}
