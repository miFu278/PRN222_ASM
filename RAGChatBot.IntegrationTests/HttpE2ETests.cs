using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Enums;
using Xunit;

namespace RAGChatBot.IntegrationTests;

[Collection(E2ECollection.Name)]
public sealed class HttpE2ETests(E2ETestFixture fixture)
{
    [Fact]
    public async Task AnonymousApiRequest_Returns401_InsteadOfLoginHtml()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(
            "/api/chat/threads", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task PasswordLogin_IssuesCookie_AndAllowsProtectedApiAccess()
    {
        const string password = "E2E-login-password";
        var user = await fixture.AddUserAsync(
            $"login-{Guid.NewGuid():N}", password, SystemRoleIds.Student);
        using var client = fixture.CreateClient();

        using var loginResponse = await LoginAsync(client, user.Username, password);
        using var protectedResponse = await client.GetAsync(
            "/api/chat/threads", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/", loginResponse.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task LecturerOpeningAdminDashboard_RendersAccessDeniedPage_InsteadOf404()
    {
        const string password = "E2E-access-denied-password";
        var lecturer = await fixture.AddUserAsync(
            $"access-denied-{Guid.NewGuid():N}", password, SystemRoleIds.Lecturer);
        using var client = fixture.CreateClient();
        using var loginResponse = await LoginAsync(client, lecturer.Username, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        using var dashboardResponse = await client.GetAsync(
            "/Admin/Dashboard", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, dashboardResponse.StatusCode);
        Assert.Equal("/Account/AccessDenied", dashboardResponse.Headers.Location?.AbsolutePath);

        using var accessDeniedResponse = await client.GetAsync(
            dashboardResponse.Headers.Location, TestContext.Current.CancellationToken);
        var html = await accessDeniedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, accessDeniedResponse.StatusCode);
        Assert.Contains("fa-shield-halved", html);
        Assert.Contains("history.back()", html);
    }

    [Fact]
    public async Task ChatApi_UsesRealCookieServiceRepositoryAndPostgres_EndToEnd()
    {
        const string password = "E2E-chat-password";
        var user = await fixture.AddUserAsync(
            $"chat-{Guid.NewGuid():N}", password, SystemRoleIds.Student);
        var courseCode = $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await AddCourseAsync(courseCode, user.Id);
        using var client = fixture.CreateClient();
        using var loginResponse = await LoginAsync(client, user.Username, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, "/Subscription/Checkout");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new { message = "Explain DbContext", courseCode })
        };
        request.Headers.Add("RequestVerificationToken", antiforgeryToken);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("E2E answer", payload.RootElement.GetProperty("reply").GetString());
        Assert.Equal(9, payload.RootElement.GetProperty("remaining").GetInt32());
        var threadId = payload.RootElement.GetProperty("threadId").GetGuid();

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.ChatMessages.CountAsync(
            message => message.ThreadId == threadId, TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.ChatSessions.SingleAsync(
            session => session.UserId == user.Id, TestContext.Current.CancellationToken)
            .ContinueWith(task => task.Result.MessageCount, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QuizApi_StartAndSubmit_RunsCompleteStudentFlowThroughHttp()
    {
        const string password = "E2E-quiz-password";
        var user = await fixture.AddUserAsync(
            $"quiz-http-{Guid.NewGuid():N}", password, SystemRoleIds.Student);
        var courseCode = $"Q{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var quiz = await AddQuizAggregateAsync(courseCode, user.Id);
        using var client = fixture.CreateClient();
        using var loginResponse = await LoginAsync(client, user.Username, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        var token = await GetAntiforgeryTokenAsync(client, "/Subscription/Checkout");

        using var startRequest = new HttpRequestMessage(HttpMethod.Post, "/api/quiz?handler=StartAttempt")
        {
            Content = JsonContent.Create(new { quizId = quiz.QuizId })
        };
        startRequest.Headers.Add("RequestVerificationToken", token);
        using var startResponse = await client.SendAsync(startRequest, TestContext.Current.CancellationToken);
        var startPayload = JsonDocument.Parse(await startResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var attemptId = startPayload.RootElement.GetProperty("attemptId").GetGuid();
        var questionId = startPayload.RootElement.GetProperty("questions")[0].GetProperty("id").GetGuid();

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/quiz?handler=Submit")
        {
            Content = JsonContent.Create(new
            {
                attemptId,
                answers = new[] { new { questionId, selectedAnswer = "A" } }
            })
        };
        submitRequest.Headers.Add("RequestVerificationToken", token);
        using var submitResponse = await client.SendAsync(submitRequest, TestContext.Current.CancellationToken);
        var submitPayload = JsonDocument.Parse(await submitResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        Assert.Equal(1, submitPayload.RootElement.GetProperty("score").GetInt32());
        Assert.Equal(100d, submitPayload.RootElement.GetProperty("percentage").GetDouble());
    }

    [Fact]
    public async Task LecturerUpload_StoresMetadataAndPrivateFile_ThenManagerCanDownload()
    {
        const string password = "E2E-document-password";
        var lecturer = await fixture.AddUserAsync(
            $"lecturer-{Guid.NewGuid():N}", password, SystemRoleIds.Lecturer);
        var courseCode = $"D{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await AddCourseAsync(courseCode, lecturer.Id, lecturer.Id);
        using var client = fixture.CreateClient();
        using var loginResponse = await LoginAsync(client, lecturer.Username, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        var path = $"/Courses/{courseCode}/Documents";
        var token = await GetAntiforgeryTokenAsync(client, path);
        var fileBytes = "integration document content"u8.ToArray();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        form.Add(new StringContent(courseCode), "CourseCode");
        form.Add(new StringContent("Chapter E2E"), "Chapter");
        form.Add(new StringContent("Character"), "ChunkingStrategy");
        form.Add(new StringContent("500"), "ChunkSize");
        form.Add(new StringContent("50"), "Overlap");
        form.Add(new ByteArrayContent(fileBytes), "UploadedFile", "lecture.pdf");

        using var uploadResponse = await client.PostAsync(
            $"{path}?handler=Upload", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, uploadResponse.StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await db.KnowledgeDocuments.SingleAsync(
            item => item.CourseCode == courseCode, TestContext.Current.CancellationToken);
        Assert.Equal(DocumentStatus.Pending, document.Status);
        Assert.False(document.IsApproved);
        Assert.StartsWith("e2e/", document.StoragePath);

        using var downloadResponse = await client.GetAsync(
            $"{path}?handler=Download&id={document.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(fileBytes, await downloadResponse.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken));

        var viewPath = $"/Courses/{courseCode}/Documents/{document.Id}/View";
        using var viewResponse = await client.GetAsync(viewPath, TestContext.Current.CancellationToken);
        var viewHtml = await viewResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, viewResponse.StatusCode);
        Assert.Contains("lecture.pdf", viewHtml);
        Assert.Contains("handler=Content", viewHtml);
        Assert.Contains("handler=Download", viewHtml);

        using var inlineResponse = await client.GetAsync(
            $"{viewPath}?handler=Content", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, inlineResponse.StatusCode);
        Assert.Equal("application/pdf", inlineResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", inlineResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(fileBytes, await inlineResponse.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken));

        using var docxForm = new MultipartFormDataContent();
        docxForm.Add(new StringContent(token), "__RequestVerificationToken");
        docxForm.Add(new StringContent(courseCode), "CourseCode");
        docxForm.Add(new StringContent("Chapter DOCX"), "Chapter");
        docxForm.Add(new StringContent("Character"), "ChunkingStrategy");
        docxForm.Add(new StringContent("500"), "ChunkSize");
        docxForm.Add(new StringContent("50"), "Overlap");
        docxForm.Add(
            new ByteArrayContent(CreateMinimalDocx("DOCX preview content")),
            "UploadedFile",
            "preview.docx");
        using var docxUploadResponse = await client.PostAsync(
            $"{path}?handler=Upload", docxForm, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, docxUploadResponse.StatusCode);

        var docxDocument = await db.KnowledgeDocuments.SingleAsync(
            item => item.CourseCode == courseCode && item.FileName == "preview.docx",
            TestContext.Current.CancellationToken);

        document.Status = DocumentStatus.Success;
        document.IsApproved = true;
        docxDocument.Status = DocumentStatus.Success;
        docxDocument.IsApproved = true;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string studentPassword = "E2E-document-student-password";
        var student = await fixture.AddUserAsync(
            $"document-student-{Guid.NewGuid():N}", studentPassword, SystemRoleIds.Student);
        using var studentClient = fixture.CreateClient();
        using var studentLoginResponse = await LoginAsync(studentClient, student.Username, studentPassword);
        Assert.Equal(HttpStatusCode.Redirect, studentLoginResponse.StatusCode);

        using var studentDocumentsResponse = await studentClient.GetAsync(
            path, TestContext.Current.CancellationToken);
        var studentDocumentsHtml = await studentDocumentsResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, studentDocumentsResponse.StatusCode);
        Assert.Contains(viewPath, studentDocumentsHtml);
        Assert.Contains("handler=Download", studentDocumentsHtml);
        Assert.DoesNotContain("class=\"zen-action-link btn-view-chunks\"", studentDocumentsHtml);

        using var studentViewResponse = await studentClient.GetAsync(
            viewPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, studentViewResponse.StatusCode);

        var docxViewPath = $"/Courses/{courseCode}/Documents/{docxDocument.Id}/View";
        using var studentDocxViewResponse = await studentClient.GetAsync(
            docxViewPath, TestContext.Current.CancellationToken);
        var studentDocxViewHtml = await studentDocxViewResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, studentDocxViewResponse.StatusCode);
        Assert.Contains("DOCX preview content", studentDocxViewHtml);
    }

    [Fact]
    public async Task Admin_CanCreateEditDeleteCourse_ButCannotOpenDocumentsOrQuiz()
    {
        const string adminPassword = "E2E-admin-password";
        var admin = await fixture.AddUserAsync(
            $"admin-{Guid.NewGuid():N}", adminPassword, SystemRoleIds.Admin);
        var lecturer = await fixture.AddUserAsync(
            $"lecturer-admin-flow-{Guid.NewGuid():N}", "unused-password", SystemRoleIds.Lecturer);
        var courseCode = $"A{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var courseId = await AddCourseAsync(courseCode, admin.Id, lecturer.Id);
        using var client = fixture.CreateClient();
        using var loginResponse = await LoginAsync(client, admin.Username, adminPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        using var coursesResponse = await client.GetAsync("/Courses", TestContext.Current.CancellationToken);
        var coursesHtml = await coursesResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, coursesResponse.StatusCode);
        Assert.Contains("Chỉnh sửa môn học", coursesHtml);
        Assert.Contains("Xóa môn", coursesHtml);
        Assert.DoesNotContain($"/Courses/{courseCode}/Documents", coursesHtml);
        Assert.DoesNotContain($"/Courses/{courseCode}/Quiz", coursesHtml);

        using var documentsResponse = await client.GetAsync(
            $"/Courses/{courseCode}/Documents", TestContext.Current.CancellationToken);
        using var quizResponse = await client.GetAsync(
            $"/Courses/{courseCode}/Quiz", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, documentsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, quizResponse.StatusCode);
        Assert.Equal("/Account/AccessDenied", documentsResponse.Headers.Location?.AbsolutePath);
        Assert.Equal("/Account/AccessDenied", quizResponse.Headers.Location?.AbsolutePath);

        var token = await GetAntiforgeryTokenAsync(client, "/Courses");
        using var editResponse = await client.PostAsync(
            "/Courses?handler=Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = courseId.ToString(),
                ["Code"] = courseCode,
                ["Name"] = "Updated by admin",
                ["Description"] = "Admin may edit course metadata",
                ["SubjectLeaderId"] = lecturer.Id.ToString(),
                ["__RequestVerificationToken"] = token
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = await db.Courses.SingleAsync(
                course => course.Id == courseId, TestContext.Current.CancellationToken);
            Assert.Equal("Updated by admin", updated.Name);
            Assert.Equal(lecturer.Id, updated.SubjectLeaderId);
        }

        using var deleteResponse = await client.PostAsync(
            "/Courses?handler=Delete",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = courseId.ToString(),
                ["__RequestVerificationToken"] = token
            }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Courses.AnyAsync(
                course => course.Id == courseId, TestContext.Current.CancellationToken));
        }
    }

    private async Task<Guid> AddCourseAsync(string courseCode, Guid createdBy, Guid? subjectLeaderId = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course
        {
            Id = Guid.NewGuid(), Code = courseCode, Name = $"Course {courseCode}",
            CreatedBy = createdBy, SubjectLeaderId = subjectLeaderId
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return course.Id;
    }

    private async Task<(Guid QuizId, Guid QuestionId)> AddQuizAggregateAsync(string courseCode, Guid createdBy)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var documentId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var quizId = Guid.NewGuid();
        db.Courses.Add(new Course
        {
            Id = Guid.NewGuid(), Code = courseCode, Name = $"Course {courseCode}", CreatedBy = createdBy
        });
        db.KnowledgeDocuments.Add(new KnowledgeDocument
        {
            Id = documentId, FileName = "quiz.pdf", StoragePath = "e2e/quiz.pdf",
            CourseCode = courseCode, Chapter = "1", FileSize = 1, UploadedBy = createdBy,
            UploaderName = "E2E", Status = DocumentStatus.Success, IsApproved = true
        });
        db.QuestionBanks.Add(new QuestionBank
        {
            Id = questionId, DocumentId = documentId, CourseCode = courseCode, Chapter = "1",
            QuestionText = "Correct option?", OptionA = "Yes", OptionB = "No",
            OptionC = "Maybe", OptionD = "Never", CorrectAnswer = "A"
        });
        db.Quizzes.Add(new Quiz
        {
            Id = quizId, Title = "E2E Quiz", CourseCode = courseCode, DocumentId = documentId,
            QuestionCount = 1, MaxAttempts = 1, DurationMinutes = 10,
            IsPublished = true, ShuffleQuestions = false, ShuffleOptions = false,
            ReviewPolicy = QuizReviewPolicy.FullReview
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (quizId, questionId);
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string username, string password)
    {
        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        }), TestContext.Current.CancellationToken);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase);
        var value = Regex.Match(input.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(value.Success, $"Antiforgery token was not found at {path}.");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    private static byte[] CreateMinimalDocx(string text)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentEntry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(documentEntry.Open(), new UTF8Encoding(false));
            writer.Write($"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body><w:p><w:r><w:t>{System.Security.SecurityElement.Escape(text)}</w:t></w:r></w:p></w:body>
                </w:document>
                """);
        }
        return output.ToArray();
    }
}
