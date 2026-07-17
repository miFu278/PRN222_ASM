using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Enums;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class DocumentPermissionTests
{
    private readonly IFileStorageService _files = Substitute.For<IFileStorageService>();
    private readonly IKnowledgeDocumentRepository _documents = Substitute.For<IKnowledgeDocumentRepository>();
    private readonly ICourseRepository _courses = Substitute.For<ICourseRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDocumentEventService _events = Substitute.For<IDocumentEventService>();

    [Fact]
    public async Task AssignedLecturer_CanApproveDocument()
    {
        var lecturer = NewUser(RoleNames.Lecturer);
        var document = NewDocument();
        Arrange(document, lecturer, lecturer.Id);

        await CreateService().ApproveDocumentAsync(document.Id, lecturer.Id);

        Assert.True(document.IsApproved);
        await _documents.Received(1).SaveChangesAsync();
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Student)]
    public async Task NonLecturer_CannotApproveDocumentEvenWhenAssigned(string role)
    {
        var user = NewUser(role);
        var document = NewDocument();
        Arrange(document, user, user.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().ApproveDocumentAsync(document.Id, user.Id));

        Assert.False(document.IsApproved);
        await _documents.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task PreviousUploader_CannotDeleteAfterCourseIsReassigned()
    {
        var previousLecturer = NewUser(RoleNames.Lecturer);
        var document = NewDocument();
        document.UploadedBy = previousLecturer.Id;
        Arrange(document, previousLecturer, Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().DeleteDocumentAsync(document.Id, previousLecturer.Id));

        await _documents.DidNotReceive().DeleteAsync(Arg.Any<KnowledgeDocument>());
        await _files.DidNotReceive().DeleteFileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Reindex_OnlyQueuesApprovedCompletedOrFailedDocuments()
    {
        var lecturer = NewUser(RoleNames.Lecturer);
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN222", SubjectLeaderId = lecturer.Id };
        var success = NewDocument(DocumentStatus.Success, approved: true);
        var failed = NewDocument(DocumentStatus.Failed, approved: true);
        var pending = NewDocument(DocumentStatus.Pending, approved: true);
        var unapproved = NewDocument(DocumentStatus.Success, approved: false);
        _courses.GetByCodeAsync(course.Code).Returns(course);
        _users.GetByIdAsync(lecturer.Id).Returns(lecturer);
        _documents.GetByCourseCodeAsync(course.Code).Returns(new[] { success, failed, pending, unapproved });
        _documents.GetByIdAsync(success.Id).Returns(success);
        _documents.GetByIdAsync(failed.Id).Returns(failed);

        var count = await CreateService().ReindexCourseDocumentsAsync(course.Code, lecturer.Id);

        Assert.Equal(2, count);
        Assert.Equal(DocumentStatus.Pending, success.Status);
        Assert.Equal(DocumentStatus.Pending, failed.Status);
        Assert.Equal(DocumentStatus.Pending, pending.Status);
        Assert.Equal(DocumentStatus.Success, unapproved.Status);
        await _documents.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Admin_CannotReindexCourseDocuments()
    {
        var admin = NewUser(RoleNames.Admin);
        var course = new Course { Id = Guid.NewGuid(), Code = "PRN222", SubjectLeaderId = admin.Id };
        _courses.GetByCodeAsync(course.Code).Returns(course);
        _users.GetByIdAsync(admin.Id).Returns(admin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().ReindexCourseDocumentsAsync(course.Code, admin.Id));

        await _documents.DidNotReceive().GetByCourseCodeAsync(Arg.Any<string>());
    }

    private void Arrange(KnowledgeDocument document, User user, Guid subjectLeaderId)
    {
        _documents.GetByIdAsync(document.Id).Returns(document);
        _users.GetByIdAsync(user.Id).Returns(user);
        _courses.GetAllAsync().Returns(new[]
        {
            new Course { Id = Guid.NewGuid(), Code = document.CourseCode, SubjectLeaderId = subjectLeaderId }
        });
    }

    private DocumentService CreateService() => new(
        _files, _documents, _courses, _users, _events, NullLogger<DocumentService>.Instance);

    private static User NewUser(string role) => new()
    {
        Id = Guid.NewGuid(),
        Username = "user@fpt.edu.vn",
        RoleId = Guid.NewGuid(),
        Role = new Role { Id = Guid.NewGuid(), Name = role }
    };

    private static KnowledgeDocument NewDocument(
        DocumentStatus status = DocumentStatus.Pending,
        bool approved = false) => new()
    {
        Id = Guid.NewGuid(),
        FileName = "lecture.pdf",
        StoragePath = "private/lecture.pdf",
        CourseCode = "PRN222",
        Chapter = "1",
        UploadedBy = Guid.NewGuid(),
        Status = status,
        IsApproved = approved
    };
}
