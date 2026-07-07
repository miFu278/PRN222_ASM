using RAGChatBot.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.DAL.Interfaces
{
    public interface IQuizService
    {
        Task GenerateQuizForDocumentAsync(Guid documentId);
        Task<IEnumerable<QuestionBank>> GetQuizByCourseAsync(string courseCode);
        Task<QuizAttempt> SubmitQuizAttemptAsync(Guid userId, string courseCode, List<UserAnswerDto> answers);
    }
}
