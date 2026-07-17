using NSubstitute;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IPasswordHasher _passwords = Substitute.For<IPasswordHasher>();
    private readonly IEmailService _emails = Substitute.For<IEmailService>();

    private AuthService CreateService() => new(_users, _roles, _passwords, _emails);

    [Fact]
    public async Task Login_ReturnsMappedUser_WhenCredentialsAreValid()
    {
        var user = NewUser(role: RoleNames.Student);
        _users.GetByUsernameAsync("student@fpt.edu.vn").Returns(user);
        _passwords.Verify("secret", user.PasswordHash).Returns(true);

        var result = await CreateService().LoginAsync(new LoginRequest
        {
            Username = "student@fpt.edu.vn",
            Password = "secret"
        });

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(RoleNames.Student, result.Role);
    }

    [Fact]
    public async Task Login_ReturnsNull_AndDoesNotVerify_WhenUserDoesNotExist()
    {
        _users.GetByUsernameAsync(Arg.Any<string>()).Returns((User?)null);

        var result = await CreateService().LoginAsync(new LoginRequest
        {
            Username = "missing",
            Password = "secret"
        });

        Assert.Null(result);
        _passwords.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Register_HashesPassword_TrimsName_AndPersistsUser()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = RoleNames.Student };
        _users.GetByUsernameAsync("new-user").Returns((User?)null);
        _roles.GetByNameAsync(RoleNames.Student).Returns(role);
        _passwords.Hash("secret").Returns("hashed");
        User? added = null;
        _users.AddAsync(Arg.Do<User>(user => added = user)).Returns(Task.CompletedTask);

        var result = await CreateService().RegisterAsync(
            "new-user", "secret", RoleNames.Student, "Free", "  Nguyễn Văn A  ");

        Assert.NotNull(added);
        Assert.Equal("hashed", added.PasswordHash);
        Assert.Equal("Nguyễn Văn A", added.FullName);
        Assert.Equal(role.Id, added.RoleId);
        Assert.Equal(added.Id, result.Id);
        await _users.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Register_RejectsDuplicateUsername_WithoutWriting()
    {
        _users.GetByUsernameAsync("duplicate").Returns(NewUser());

        await Assert.ThrowsAsync<Exception>(() => CreateService().RegisterAsync(
            "duplicate", "secret", RoleNames.Student, "Free", "User"));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
        await _users.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ToggleSubscription_EnablesPremiumWithOneMonthExpiry()
    {
        var user = NewUser();
        user.SubscriptionTier = "Free";
        _users.GetByIdAsync(user.Id).Returns(user);
        var before = DateTime.UtcNow.AddMonths(1);

        var changed = await CreateService().ToggleSubscriptionTierAsync(user.Id);

        var after = DateTime.UtcNow.AddMonths(1);
        Assert.True(changed);
        Assert.Equal("Premium", user.SubscriptionTier);
        Assert.NotNull(user.SubscriptionExpiresAt);
        Assert.InRange(user.SubscriptionExpiresAt.Value, before, after);
        await _users.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteUser_RejectsAdminAccount()
    {
        var admin = NewUser(role: RoleNames.Admin);
        _users.GetByIdAsync(admin.Id).Returns(admin);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().DeleteUserAsync(admin.Id));

        await _users.DidNotReceive().DeleteAsync(Arg.Any<User>());
    }

    private static User NewUser(string role = RoleNames.Student) => new()
    {
        Id = Guid.NewGuid(),
        Username = "student@fpt.edu.vn",
        PasswordHash = "hash",
        FullName = "Student",
        RoleId = Guid.NewGuid(),
        Role = new Role { Id = Guid.NewGuid(), Name = role }
    };
}
