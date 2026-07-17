using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Enums;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class DocumentServiceTests
{
    private readonly IFileStorageService _files = Substitute.For<IFileStorageService>();
    private readonly IKnowledgeDocumentRepository _documents = Substitute.For<IKnowledgeDocumentRepository>();
    private readonly ICourseRepository _courses = Substitute.For<ICourseRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDocumentEventService _events = Substitute.For<IDocumentEventService>();

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("archive.zip")]
    [InlineData("no-extension")]
    public async Task Upload_RejectsUnsupportedFileType_BeforeReadingUser(string fileName)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => UploadAsync(fileName: fileName));

        await _users.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
        await _files.DidNotReceive().SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Upload_RejectsUserWhoDoesNotManageCourse()
    {
        var user = NewUser(RoleNames.Lecturer);
        _users.GetByIdAsync(user.Id).Returns(user);
        _courses.GetAllAsync().Returns(new[] { new Course { Code = "PRN222", SubjectLeaderId = Guid.NewGuid() } });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UploadAsync(userId: user.Id));

        await _files.DidNotReceive().SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Upload_FreeUserCannotExceedFiveMegabytes()
    {
        var user = NewUser(RoleNames.Lecturer);
        user.SubscriptionTier = "Free";
        ArrangeAuthorizedUpload(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            UploadAsync(userId: user.Id, fileSize: 5 * 1024 * 1024 + 1));

        await _files.DidNotReceive().SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Upload_ActivePremiumAllowsLargeFile_SanitizesName_AndDoesNotLeakStoragePath()
    {
        var user = NewUser(RoleNames.Lecturer);
        user.SubscriptionTier = "Premium";
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(1);
        ArrangeAuthorizedUpload(user);
        _files.SaveFileAsync(Arg.Any<Stream>(), "lecture.pdf").Returns("private/secret-id.pdf");
        KnowledgeDocument? added = null;
        _documents.AddAsync(Arg.Do<KnowledgeDocument>(document => added = document)).Returns(Task.CompletedTask);

        var result = await UploadAsync(
            userId: user.Id,
            fileName: "../unsafe/lecture.pdf",
            fileSize: 20 * 1024 * 1024);

        Assert.NotNull(added);
        Assert.Equal("lecture.pdf", added.FileName);
        Assert.Equal("private/secret-id.pdf", added.StoragePath);
        Assert.Equal(string.Empty, result.StoragePath);
        Assert.Equal(DocumentStatus.Pending, result.Status);
        await _events.Received(1).NotifyDocumentChangedAsync(
            Arg.Is<RealtimeChangeEvent>(change => change != null && change.Type == "DocumentCreated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_DatabaseFailure_DeletesOrphanedStoredFile()
    {
        var user = NewUser(RoleNames.Lecturer);
        ArrangeAuthorizedUpload(user);
        _files.SaveFileAsync(Arg.Any<Stream>(), "lecture.pdf").Returns("private/orphan.pdf");
        _documents.SaveChangesAsync().Returns<Task>(_ => throw new InvalidOperationException("database failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => UploadAsync(userId: user.Id));

        await _files.Received(1).DeleteFileAsync("private/orphan.pdf");
    }

    [Fact]
    public async Task Download_DeniesOrdinaryUser_WhenDocumentIsNotApprovedAndSuccessful()
    {
        var user = NewUser(RoleNames.Student);
        var document = NewDocument();
        document.IsApproved = false;
        document.Status = DocumentStatus.Pending;
        _documents.GetByIdAsync(document.Id).Returns(document);
        _users.GetByIdAsync(user.Id).Returns(user);
        _courses.GetByCodeAsync(document.CourseCode).Returns(new Course { Code = document.CourseCode });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().DownloadDocumentAsync(document.Id, user.Id));

        await _files.DidNotReceive().OpenReadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Download_AllowsOrdinaryUser_ForApprovedSuccessfulDocument()
    {
        var user = NewUser(RoleNames.Student);
        var document = NewDocument();
        document.IsApproved = true;
        document.Status = DocumentStatus.Success;
        var content = new MemoryStream(new byte[] { 1, 2, 3 });
        _documents.GetByIdAsync(document.Id).Returns(document);
        _users.GetByIdAsync(user.Id).Returns(user);
        _courses.GetByCodeAsync(document.CourseCode).Returns(new Course { Code = document.CourseCode });
        _files.OpenReadAsync(document.StoragePath).Returns(content);

        var result = await CreateService().DownloadDocumentAsync(document.Id, user.Id);

        Assert.Same(content, result.Content);
        Assert.Equal(document.FileName, result.FileName);
    }

    [Fact]
    public async Task Upload_DeniesAdmin_EvenWhenAssignedToCourse()
    {
        var user = NewUser(RoleNames.Admin);
        ArrangeAuthorizedUpload(user);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => UploadAsync(userId: user.Id));

        await _files.DidNotReceive().SaveFileAsync(Arg.Any<Stream>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Download_DeniesAdmin_ForApprovedSuccessfulDocument()
    {
        var user = NewUser(RoleNames.Admin);
        var document = NewDocument();
        document.IsApproved = true;
        document.Status = DocumentStatus.Success;
        _documents.GetByIdAsync(document.Id).Returns(document);
        _users.GetByIdAsync(user.Id).Returns(user);
        _courses.GetByCodeAsync(document.CourseCode).Returns(new Course
        {
            Code = document.CourseCode,
            SubjectLeaderId = user.Id
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().DownloadDocumentAsync(document.Id, user.Id));

        await _files.DidNotReceive().OpenReadAsync(Arg.Any<string>());
    }

    private Task<RAGChatBot.BLL.DTOs.DocumentDto> UploadAsync(
        Guid? userId = null,
        string fileName = "lecture.pdf",
        long fileSize = 1024)
        => CreateService().UploadDocumentAsync(
            new MemoryStream(new byte[] { 1 }), fileName, fileSize,
            " prn222 ", " Chapter 1 ", userId ?? Guid.NewGuid());

    private void ArrangeAuthorizedUpload(User user)
    {
        _users.GetByIdAsync(user.Id).Returns(user);
        _courses.GetAllAsync().Returns(new[]
        {
            new Course { Code = "PRN222", SubjectLeaderId = user.Id }
        });
    }

    private DocumentService CreateService() => new(
        _files, _documents, _courses, _users, _events, NullLogger<DocumentService>.Instance);

    private static User NewUser(string role) => new()
    {
        Id = Guid.NewGuid(),
        Username = "user@fpt.edu.vn",
        FullName = "User",
        Role = new Role { Id = Guid.NewGuid(), Name = role },
        RoleId = Guid.NewGuid(),
        SubscriptionTier = "Free"
    };

    private static KnowledgeDocument NewDocument() => new()
    {
        Id = Guid.NewGuid(),
        FileName = "lecture.pdf",
        StoragePath = "private/lecture.pdf",
        CourseCode = "PRN222",
        Chapter = "1",
        UploadedBy = Guid.NewGuid()
    };
}
