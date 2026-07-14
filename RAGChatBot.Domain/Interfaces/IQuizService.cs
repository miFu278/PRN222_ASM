using RAGChatBot.Domain.Models;
using RAGChatBot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IQuizService
    {
        Task GenerateQuizForDocumentAsync(Guid documentId);
        Task<IReadOnlyList<QuizQuestionModel>> GetQuizByCourseAsync(string courseCode);
        Task<QuizStartResult> StartQuizAttemptAsync(Guid userId, Guid quizId, string? password);
        Task<QuizAttemptResult> SubmitQuizAttemptAsync(Guid userId, Guid attemptId, IReadOnlyList<UserAnswerDto> answers);
        Task<IReadOnlyList<QuestionBank>> GetQuestionBankByCourseAsync(string courseCode);
        Task<IReadOnlyList<QuizAttemptDetailsDto>> GetAttemptsByCourseAsync(string courseCode);
        Task<QuestionBank> AddQuestionAsync(QuestionBank question);
        Task<QuestionBank> UpdateQuestionAsync(QuestionBank question);
        Task DeleteQuestionAsync(Guid id, string courseCode);
        Task<IReadOnlyList<QuizSummaryModel>> GetQuizzesByCourseAsync(string courseCode, Guid? userId = null, bool includeUnpublished = false);
        Task<Quiz> CreateQuizAsync(string courseCode, string title, int questionCount, Guid? documentId,
            int maxAttempts, int durationMinutes, string? password, QuizReviewPolicy reviewPolicy,
            bool shuffleQuestions, bool shuffleOptions);
        Task DeleteQuizAsync(Guid id, string courseCode);
        Task<IReadOnlyList<QuizAttemptDetailsDto>> GetStudentAttemptsAsync(Guid userId, string? courseCode = null);
        Task<QuizReviewModel> GetAttemptReviewAsync(Guid requesterId, Guid attemptId, bool instructorView, string? managedCourseCode = null);
    }
}
