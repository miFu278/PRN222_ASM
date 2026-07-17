using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Constants;
using Xunit;

namespace RAGChatBot.IntegrationTests;

[Collection(E2ECollection.Name)]
public sealed class MigrationIntegrationTests(E2ETestFixture fixture)
{
    [Fact]
    public async Task Startup_AppliesAllMigrations_AndSeedsSystemRoles()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Contains("Database=ragchatbot_e2e", db.Database.GetConnectionString());
        var pending = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var roles = await db.Roles.OrderBy(role => role.Name).Select(role => role.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(pending);
        Assert.Equal(new[] { RoleNames.Admin, RoleNames.Lecturer, RoleNames.Student }.Order(), roles);
    }

    [Fact]
    public async Task Database_HasVectorExtension_AndHardenedIndexes()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vectorInstalled = await db.Database
            .SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);
        var indexNames = await db.Database
            .SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.True(vectorInstalled);
        Assert.Contains("IX_ChatSessions_UserId_UsageDate", indexNames);
        Assert.Contains("IX_QuizAttempts_UserId_QuizId", indexNames);
        Assert.Contains("IX_PaymentTransactions_OrderId", indexNames);
    }
}
