using RAGChatBot.DAL.Entities;

namespace RAGChatBot.DAL.Interfaces
{
    public interface IBenchmarkRepository
    {
        Task AddAsync(PerformanceBenchmark benchmark);
        Task<List<PerformanceBenchmark>> GetRecentAsync(int count = 100);
        Task<Dictionary<string, double>> GetAveragesByTypeAsync();
    }
}
