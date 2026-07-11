using RAGChatBot.Domain.Models;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IQuizGenerationService
    {
        Task<IReadOnlyList<GeneratedQuizQuestion>> GenerateQuestionsAsync(string prompt);
    }
}
