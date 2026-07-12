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
        Task<QuizAttemptResult> SubmitQuizAttemptAsync(
            Guid userId,
            string courseCode,
            Guid? quizId,
            IReadOnlyList<UserAnswerDto> answers);
        Task<IReadOnlyList<QuestionBank>> GetQuestionBankByCourseAsync(string courseCode);
        Task<IReadOnlyList<QuizAttemptDetailsDto>> GetAttemptsByCourseAsync(string courseCode);
        Task<QuestionBank> AddQuestionAsync(QuestionBank question);
        Task<QuestionBank> UpdateQuestionAsync(QuestionBank question);
        Task DeleteQuestionAsync(Guid id);
        Task<IReadOnlyList<Quiz>> GetQuizzesByCourseAsync(string courseCode);
        Task<Quiz> CreateQuizAsync(string courseCode, string title, int questionCount, Guid? documentId);
        Task DeleteQuizAsync(Guid id);
        Task<IReadOnlyList<QuizQuestionModel>> GetQuizQuestionsAsync(Guid quizId);
    }
}
