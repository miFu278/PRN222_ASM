using RAGChatBot.Domain.Models;
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
            IReadOnlyList<UserAnswerDto> answers);
    }
}
