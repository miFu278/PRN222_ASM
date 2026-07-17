using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class WhitelistServiceTests
{
    private readonly IWhitelistRepository _repository = Substitute.For<IWhitelistRepository>();
    private readonly IEmailService _emails = Substitute.For<IEmailService>();

    [Fact]
    public async Task IsWhitelisted_BlankEmailReturnsFalseWithoutRepositoryCall()
    {
        Assert.False(await CreateService().IsEmailWhitelistedAsync("  "));
        await _repository.DidNotReceive().GetByEmailAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task IsWhitelisted_NormalizesEmailBeforeLookup()
    {
        _repository.GetByEmailAsync("student@fpt.edu.vn")
            .Returns(new WhitelistEmail { Email = "student@fpt.edu.vn" });

        var result = await CreateService().IsEmailWhitelistedAsync("  Student@FPT.EDU.VN ");

        Assert.True(result);
        await _repository.Received(1).GetByEmailAsync("student@fpt.edu.vn");
    }

    [Fact]
    public async Task Add_RejectsDuplicateWithoutWritingOrEmailing()
    {
        _repository.GetByEmailAsync("student@fpt.edu.vn")
            .Returns(new WhitelistEmail { Email = "student@fpt.edu.vn" });

        await Assert.ThrowsAsync<Exception>(() =>
            CreateService().AddAsync("Student@FPT.EDU.VN", "Student", "SE123"));

        await _repository.DidNotReceive().AddAsync(Arg.Any<WhitelistEmail>());
        await _emails.DidNotReceive().SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Add_NormalizesAndPersistsBeforeSendingWelcomeEmail()
    {
        WhitelistEmail? added = null;
        _repository.AddAsync(Arg.Do<WhitelistEmail>(item => added = item)).Returns(Task.CompletedTask);

        await CreateService().AddAsync("  Student@FPT.EDU.VN  ", "  Nguyen Van A  ", "  SE123  ");

        Assert.NotNull(added);
        Assert.Equal("student@fpt.edu.vn", added.Email);
        Assert.Equal("Nguyen Van A", added.FullName);
        Assert.Equal("SE123", added.StudentId);
        await _repository.Received(1).SaveChangesAsync();
        await _emails.Received(1).SendWelcomeEmailAsync("student@fpt.edu.vn", "Nguyen Van A");
    }

    [Fact]
    public async Task Add_EmailFailureDoesNotRollbackPersistedWhitelistEntry()
    {
        _emails.SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns<Task>(_ => throw new InvalidOperationException("email unavailable"));

        await CreateService().AddAsync("student@fpt.edu.vn", "Student", "SE123");

        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Delete_MissingEntryThrowsWithoutWriting()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id).Returns((WhitelistEmail?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateService().DeleteAsync(id));

        await _repository.DidNotReceive().DeleteAsync(Arg.Any<WhitelistEmail>());
        await _repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Delete_ExistingEntryRemovesAndSaves()
    {
        var entry = new WhitelistEmail { Id = Guid.NewGuid(), Email = "student@fpt.edu.vn" };
        _repository.GetByIdAsync(entry.Id).Returns(entry);

        await CreateService().DeleteAsync(entry.Id);

        await _repository.Received(1).DeleteAsync(entry);
        await _repository.Received(1).SaveChangesAsync();
    }

    private WhitelistService CreateService() => new(
        _repository,
        _emails,
        NullLogger<WhitelistService>.Instance);
}
