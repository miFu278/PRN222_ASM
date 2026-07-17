using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Testcontainers.PostgreSql;
using Xunit;

namespace RAGChatBot.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ECollection : ICollectionFixture<E2ETestFixture>
{
    public const string Name = "PostgreSQL E2E";
}

public sealed class E2ETestFixture : IAsyncLifetime
{
    private readonly string? _externalConnectionString;
    private PostgreSqlContainer? _postgres;
    private TestWebApplicationFactory? _factory;

    public E2ETestFixture()
    {
        _externalConnectionString = Environment.GetEnvironmentVariable(
            "RAGCHATBOT_TEST_CONNECTION_STRING");

        if (!string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            EnsureDisposableTestDatabase(_externalConnectionString);
        }
    }

    public IServiceProvider Services => Factory.Services;
    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The E2E fixture has not started.");

    public async ValueTask InitializeAsync()
    {
        var connectionString = _externalConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg16")
                .WithDatabase("ragchatbot_e2e")
                .WithUsername("postgres")
                .WithPassword("postgres-e2e-only")
                .Build();
            await _postgres.StartAsync(TestContext.Current.CancellationToken);
            connectionString = _postgres.GetConnectionString();
        }

        _factory = new TestWebApplicationFactory(connectionString);

        // Force the complete application startup path, including every EF migration.
        using var client = CreateClient(allowAutoRedirect: false);
        using var response = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    public HttpClient CreateClient(bool allowAutoRedirect = false) => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = allowAutoRedirect,
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    });

    public async Task<User> AddUserAsync(string username, string password, Guid roleId, string tier = "Free")
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = hasher.Hash(password),
            RoleId = roleId,
            FullName = username,
            SubscriptionTier = tier,
            SubscriptionExpiresAt = tier == "Premium" ? DateTime.UtcNow.AddDays(30) : null
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private static void EnsureDisposableTestDatabase(string connectionString)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "RAGCHATBOT_TEST_CONNECTION_STRING không phải connection string PostgreSQL hợp lệ.",
                exception);
        }

        var databaseName = builder.Database?.Trim();
        if (string.IsNullOrWhiteSpace(databaseName) ||
            (!databaseName.Contains("test", StringComparison.OrdinalIgnoreCase) &&
             !databaseName.Contains("e2e", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Integration tests chỉ được chạy trên database riêng có tên chứa 'test' hoặc 'e2e'. " +
                "Không được trỏ RAGCHATBOT_TEST_CONNECTION_STRING vào database production.");
        }
    }

    private sealed class TestWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Authentication:Google:ClientId"] = "e2e-google-client",
                    ["Authentication:Google:ClientSecret"] = "e2e-google-secret",
                    ["Supabase:Url"] = "http://127.0.0.1:54321",
                    ["Supabase:ServiceKey"] = "e2e-supabase-key",
                    ["PayOS:ClientId"] = "e2e-payos-client",
                    ["PayOS:ApiKey"] = "e2e-payos-key",
                    ["PayOS:ChecksumKey"] = "e2e-checksum-key-e2e-checksum-key",
                    ["AiSettings:ApiKey"] = "e2e-ai-key"
                }));

            builder.ConfigureTestServices(services =>
            {
                // Program reads configuration before WebApplicationFactory's late configuration hook.
                // Replace the already-registered context so no test can fall back to appsettings.
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(connectionString, postgres => postgres.UseVector()));

                // Prevent the polling worker and all real external I/O during the E2E suite.
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IChatResponseService>();
                services.AddSingleton<IChatResponseService, DeterministicChatResponseService>();
                services.RemoveAll<IFileStorageService>();
                services.AddSingleton<IFileStorageService, InMemoryFileStorageService>();
            });
        }
    }

    private sealed class DeterministicChatResponseService : IChatResponseService
    {
        public Task<ChatResponseResult> GetChatResponseAsync(
            string question,
            string courseCode,
            IReadOnlyList<ChatHistoryItem> history)
            => Task.FromResult(new ChatResponseResult(
                $"E2E answer for {courseCode}: {question}",
                true,
                Array.Empty<ChatSource>()));
    }

    private sealed class InMemoryFileStorageService : IFileStorageService
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();

        public async Task<string> SaveFileAsync(Stream fileStream, string fileName)
        {
            await using var copy = new MemoryStream();
            await fileStream.CopyToAsync(copy, TestContext.Current.CancellationToken);
            var path = $"e2e/{Guid.NewGuid():N}/{Path.GetFileName(fileName)}";
            _files[path] = copy.ToArray();
            return path;
        }

        public Task<Stream> OpenReadAsync(string storagePath)
            => Task.FromResult<Stream>(new MemoryStream(_files[storagePath], writable: false));

        public Task DeleteFileAsync(string storagePath)
        {
            _files.TryRemove(storagePath, out _);
            return Task.CompletedTask;
        }
    }
}
