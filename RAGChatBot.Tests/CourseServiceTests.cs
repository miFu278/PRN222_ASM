using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class CourseServiceTests
{
    private readonly ICourseRepository _courses = Substitute.For<ICourseRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IKnowledgeDocumentRepository _documents = Substitute.For<IKnowledgeDocumentRepository>();
    private readonly IFileStorageService _files = Substitute.For<IFileStorageService>();
    private readonly ICourseEventService _events = Substitute.For<ICourseEventService>();

    [Fact]
    public async Task Create_RequiresAssignedLecturer()
    {
        var dto = new CourseDto { Code = "PRN222", Name = "ASP.NET" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().CreateCourseAsync(dto, Guid.NewGuid()));

        await _courses.DidNotReceive().AddAsync(Arg.Any<Course>());
    }

    [Theory]
    [InlineData(RoleNames.Admin)]
    [InlineData(RoleNames.Student)]
    public async Task Create_RejectsNonLecturerAssignment(string role)
    {
        var assignee = NewUser(role);
        _users.GetByIdAsync(assignee.Id).Returns(assignee);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().CreateCourseAsync(
            new CourseDto
            {
                Code = "PRN222",
                Name = "ASP.NET",
                SubjectLeaderId = assignee.Id
            },
            Guid.NewGuid()));

        await _courses.DidNotReceive().AddAsync(Arg.Any<Course>());
    }

    [Fact]
    public async Task Create_NormalizesFields_PersistsAndPublishesEvent()
    {
        var creatorId = Guid.NewGuid();
        var lecturer = NewUser(RoleNames.Lecturer);
        _users.GetByIdAsync(lecturer.Id).Returns(lecturer);
        Course? added = null;
        _ = _courses.AddAsync(Arg.Do<Course>(course => added = course));

        var result = await CreateService().CreateCourseAsync(new CourseDto
        {
            Code = " prn222 ",
            Name = "  ASP.NET Core  ",
            Description = "  Web development  ",
            SubjectLeaderId = lecturer.Id
        }, creatorId);

        Assert.NotNull(added);
        Assert.Equal("PRN222", added.Code);
        Assert.Equal("ASP.NET Core", added.Name);
        Assert.Equal("Web development", added.Description);
        Assert.Equal(creatorId, added.CreatedBy);
        Assert.Equal(lecturer.Id, added.SubjectLeaderId);
        Assert.Equal(added.Id, result.Id);
        await _events.Received(1).NotifyCourseChangedAsync(
            "CourseCreated", added.Id, "PRN222", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_RejectsChangingCourseCode()
    {
        var course = NewCourse();
        _courses.GetByIdAsync(course.Id).Returns(course);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().UpdateCourseAsync(new CourseDto
            {
                Id = course.Id,
                Code = "OTHER",
                Name = course.Name
            }));

        await _courses.DidNotReceive().UpdateAsync(Arg.Any<Course>());
    }

    [Fact]
    public async Task Update_ChangesMetadataAndAssignedLecturer()
    {
        var course = NewCourse();
        var lecturer = NewUser(RoleNames.Lecturer);
        _courses.GetByIdAsync(course.Id).Returns(course);
        _users.GetByIdAsync(lecturer.Id).Returns(lecturer);

        await CreateService().UpdateCourseAsync(new CourseDto
        {
            Id = course.Id,
            Code = course.Code.ToLowerInvariant(),
            Name = "  Updated course  ",
            Description = "  Updated description  ",
            SubjectLeaderId = lecturer.Id
        });

        Assert.Equal("Updated course", course.Name);
        Assert.Equal("Updated description", course.Description);
        Assert.Equal(lecturer.Id, course.SubjectLeaderId);
        await _courses.Received(1).UpdateAsync(course);
        await _events.Received(1).NotifyCourseChangedAsync(
            "CourseUpdated", course.Id, course.Code, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_RemovesAggregateAndStoredFiles_ThenPublishesEvent()
    {
        var course = NewCourse();
        var documents = new[]
        {
            new KnowledgeDocument { Id = Guid.NewGuid(), CourseCode = course.Code, StoragePath = "private/a.pdf" },
            new KnowledgeDocument { Id = Guid.NewGuid(), CourseCode = course.Code, StoragePath = "private/b.pdf" }
        };
        _courses.GetByIdAsync(course.Id).Returns(course);
        _documents.GetByCourseCodeAsync(course.Code).Returns(documents);

        await CreateService().DeleteCourseAsync(course.Id);

        await _courses.Received(1).DeleteAggregateAsync(course);
        await _files.Received(1).DeleteFileAsync("private/a.pdf");
        await _files.Received(1).DeleteFileAsync("private/b.pdf");
        await _events.Received(1).NotifyCourseChangedAsync(
            "CourseDeleted", course.Id, course.Code, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ContinuesWhenObjectStorageCleanupFails()
    {
        var course = NewCourse();
        _courses.GetByIdAsync(course.Id).Returns(course);
        _documents.GetByCourseCodeAsync(course.Code).Returns(new[]
        {
            new KnowledgeDocument { Id = Guid.NewGuid(), CourseCode = course.Code, StoragePath = "private/a.pdf" }
        });
        _files.DeleteFileAsync("private/a.pdf").Returns<Task>(_ => throw new IOException("storage unavailable"));

        await CreateService().DeleteCourseAsync(course.Id);

        await _events.Received(1).NotifyCourseChangedAsync(
            "CourseDeleted", course.Id, course.Code, Arg.Any<CancellationToken>());
    }

    private CourseService CreateService() => new(
        _courses,
        _users,
        _documents,
        _files,
        _events,
        NullLogger<CourseService>.Instance);

    private static Course NewCourse() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PRN222",
        Name = "ASP.NET Core",
        CreatedBy = Guid.NewGuid()
    };

    private static User NewUser(string role) => new()
    {
        Id = Guid.NewGuid(),
        Username = $"{role.ToLowerInvariant()}@fpt.edu.vn",
        RoleId = Guid.NewGuid(),
        Role = new Role { Id = Guid.NewGuid(), Name = role }
    };
}
