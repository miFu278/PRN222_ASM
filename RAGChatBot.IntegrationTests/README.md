# Integration and E2E tests

These tests start a disposable `pgvector/pgvector:pg16` PostgreSQL container,
apply every EF Core migration, and host the real Razor Pages application with
`WebApplicationFactory`.

Real AI, Supabase storage, email, PayOS, and background-worker I/O are replaced
with deterministic in-process test implementations. `AppDbContext` is replaced
after application service registration, so the suite cannot fall back to the
connection string in `appsettings.json`.

Prerequisite: Docker Desktop must be running.

```powershell
dotnet test .\RAGChatBot.IntegrationTests\RAGChatBot.IntegrationTests.csproj -c Release
```

The container and its database are removed automatically after the test run.
