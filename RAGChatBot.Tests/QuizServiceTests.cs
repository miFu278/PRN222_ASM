using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using Xunit;

namespace RAGChatBot.Tests;

public sealed class QuizServiceTests
{
    private readonly IQuizRepository _quizzes = Substitute.For<IQuizRepository>();
    private readonly IQuizGenerationService _generator = Substitute.For<IQuizGenerationService>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwords = Substitute.For<IPasswordHasher>();
    private readonly IQuizEventService _events = Substitute.For<IQuizEventService>();

    [Fact]
    public async Task Start_RejectsUnpublishedQuiz()
    {
        var quiz = NewQuiz();
        quiz.IsPublished = false;
        _quizzes.GetQuizByIdAsync(quiz.Id).Returns(quiz);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().StartQuizAttemptAsync(Guid.NewGuid(), quiz.Id, null));

        await _quizzes.DidNotReceive().AddAttemptAsync(Arg.Any<QuizAttempt>());
    }

    [Fact]
    public async Task Start_ReturnsExistingActiveAttempt_InsteadOfCreatingDuplicate()
    {
        var userId = Guid.NewGuid();
        var quiz = NewQuiz();
        var existing = NewAttempt(userId, quiz.Id, DateTime.UtcNow.AddMinutes(5));
        existing.Answers.Add(NewSnapshot(Guid.NewGuid(), "A"));
        _quizzes.GetQuizByIdAsync(quiz.Id).Returns(quiz);
        _quizzes.GetInProgressAttemptAsync(userId, quiz.Id).Returns(existing);

        var result = await CreateService().StartQuizAttemptAsync(userId, quiz.Id, null);

        Assert.Equal(existing.Id, result.AttemptId);
        await _quizzes.DidNotReceive().AddAttemptAsync(Arg.Any<QuizAttempt>());
    }

    [Fact]
    public async Task Start_FinalizesExpiredAttempt_BeforeCreatingNextOne()
    {
        var userId = Guid.NewGuid();
        var quiz = NewQuiz();
        var expired = NewAttempt(userId, quiz.Id, DateTime.UtcNow.AddMinutes(-1));
        _quizzes.GetQuizByIdAsync(quiz.Id).Returns(quiz);
        _quizzes.GetInProgressAttemptAsync(userId, quiz.Id).Returns(expired);
        _quizzes.GetAttemptCountAsync(userId, quiz.Id).Returns(1);
        _quizzes.GetQuestionsByCourseAsync(quiz.CourseCode).Returns(new[] { NewQuestion() });
#pragma warning disable CS8620 // NSubstitute's Task<T?> overload loses the interface's nullability annotation.
        _quizzes.AddAttemptAsync(Arg.Any<QuizAttempt>()).Returns(call => call.Arg<QuizAttempt>());
#pragma warning restore CS8620

        var result = await CreateService().StartQuizAttemptAsync(userId, quiz.Id, null);

        Assert.Equal(QuizAttemptStatus.Submitted, expired.Status);
        Assert.Equal(expired.ExpiresAt, expired.SubmittedAt);
        Assert.Equal(2, result.AttemptNumber);
        await _quizzes.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Start_RejectsWrongPassword_BeforeReadingAttemptQuota()
    {
        var quiz = NewQuiz();
        quiz.PasswordHash = "hash";
        _quizzes.GetQuizByIdAsync(quiz.Id).Returns(quiz);
        _passwords.Verify("wrong", "hash").Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().StartQuizAttemptAsync(Guid.NewGuid(), quiz.Id, "wrong"));

        await _quizzes.DidNotReceive().GetAttemptCountAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Start_CreatesImmutableQuestionSnapshots_WithDeadline()
    {
        var userId = Guid.NewGuid();
        var quiz = NewQuiz();
        quiz.DurationMinutes = 15;
        var question = NewQuestion();
        _quizzes.GetQuizByIdAsync(quiz.Id).Returns(quiz);
        _quizzes.GetQuestionsByCourseAsync(quiz.CourseCode).Returns(new[] { question });
        QuizAttempt? added = null;
#pragma warning disable CS8620 // NSubstitute's Task<T?> overload loses the interface's nullability annotation.
        _quizzes.AddAttemptAsync(Arg.Do<QuizAttempt>(attempt => added = attempt))
            .Returns(call => call.Arg<QuizAttempt>());
#pragma warning restore CS8620

        var result = await CreateService().StartQuizAttemptAsync(userId, quiz.Id, null);

        Assert.NotNull(added);
        Assert.Single(added.Answers);
        Assert.Equal(question.QuestionText, added.Answers.Single().QuestionText);
        Assert.Equal(question.CorrectAnswer, added.Answers.Single().CorrectAnswer);
        Assert.InRange((added.ExpiresAt - added.StartedAt).TotalMinutes, 14.99, 15.01);
        Assert.Equal(added.Id, result.AttemptId);
    }

    [Fact]
    public async Task Submit_RejectsAttemptOwnedByAnotherUser_WithoutSaving()
    {
        var attempt = NewAttempt(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(10));
        _quizzes.GetAttemptWithAnswersAsync(attempt.Id).Returns(attempt);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService().SubmitQuizAttemptAsync(Guid.NewGuid(), attempt.Id, Array.Empty<UserAnswerDto>()));

        await _quizzes.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_ExpiredAttempt_IsClosedWithZero_ThenRejected()
    {
        var userId = Guid.NewGuid();
        var attempt = NewAttempt(userId, Guid.NewGuid(), DateTime.UtcNow.AddSeconds(-1));
        attempt.Score = 5;
        _quizzes.GetAttemptWithAnswersAsync(attempt.Id).Returns(attempt);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().SubmitQuizAttemptAsync(userId, attempt.Id, Array.Empty<UserAnswerDto>()));

        Assert.Equal(QuizAttemptStatus.Submitted, attempt.Status);
        Assert.Equal(0, attempt.Score);
        Assert.Equal(attempt.ExpiresAt, attempt.SubmittedAt);
        await _quizzes.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_UsesLastDuplicateAnswer_NormalizesInput_AndCalculatesScore()
    {
        var userId = Guid.NewGuid();
        var attempt = NewAttempt(userId, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(10));
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        attempt.Answers.Add(NewSnapshot(firstId, "B"));
        attempt.Answers.Add(NewSnapshot(secondId, "D"));
        _quizzes.GetAttemptWithAnswersAsync(attempt.Id).Returns(attempt);

        var result = await CreateService().SubmitQuizAttemptAsync(userId, attempt.Id, new[]
        {
            new UserAnswerDto { QuestionId = firstId, SelectedAnswer = "A" },
            new UserAnswerDto { QuestionId = firstId, SelectedAnswer = " b " },
            new UserAnswerDto { QuestionId = secondId, SelectedAnswer = "invalid" }
        });

        Assert.Equal(1, result.Score);
        Assert.Equal(2, result.TotalQuestions);
        Assert.Equal(50d, result.Percentage);
        Assert.Equal("B", attempt.Answers.First().SelectedAnswer);
        Assert.Null(attempt.Answers.Last().SelectedAnswer);
        Assert.Equal(QuizAttemptStatus.Submitted, attempt.Status);
        await _events.Received(1).NotifyQuizChangedAsync(
            Arg.Is<RealtimeChangeEvent>(change => change != null && change.Type == "AttemptSubmitted"),
            Arg.Any<CancellationToken>());
    }

    private QuizService CreateService() => new(
        _quizzes, _generator, _users, _passwords, _events, NullLogger<QuizService>.Instance);

    private static Quiz NewQuiz() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Quiz",
        CourseCode = "PRN222",
        QuestionCount = 1,
        MaxAttempts = 3,
        DurationMinutes = 30,
        IsPublished = true,
        ShuffleQuestions = false,
        ShuffleOptions = false
    };

    private static QuizAttempt NewAttempt(Guid userId, Guid quizId, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        QuizId = quizId,
        CourseCode = "PRN222",
        QuizTitle = "Quiz",
        AttemptNumber = 1,
        Status = QuizAttemptStatus.InProgress,
        StartedAt = DateTime.UtcNow.AddMinutes(-1),
        ExpiresAt = expiresAt
    };

    private static QuestionBank NewQuestion() => new()
    {
        Id = Guid.NewGuid(),
        DocumentId = Guid.NewGuid(),
        CourseCode = "PRN222",
        Chapter = "1",
        QuestionText = "Question?",
        OptionA = "A",
        OptionB = "B",
        OptionC = "C",
        OptionD = "D",
        CorrectAnswer = "A"
    };

    private static QuizAttemptAnswer NewSnapshot(Guid questionId, string correctAnswer) => new()
    {
        Id = Guid.NewGuid(),
        QuestionId = questionId,
        QuestionText = "Question?",
        OptionA = "A",
        OptionB = "B",
        OptionC = "C",
        OptionD = "D",
        CorrectAnswer = correctAnswer
    };
}
