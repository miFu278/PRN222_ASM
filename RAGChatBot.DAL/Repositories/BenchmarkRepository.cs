using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public class BenchmarkRepository : IBenchmarkRepository
    {
        private readonly AppDbContext _db;
        public BenchmarkRepository(AppDbContext db) => _db = db;

        public async Task AddAsync(PerformanceBenchmark benchmark)
        {
            _db.PerformanceBenchmarks.Add(benchmark);
            await _db.SaveChangesAsync();
        }

        public async Task<List<PerformanceBenchmark>> GetRecentAsync(int count = 100)
            => await _db.PerformanceBenchmarks
                .OrderByDescending(b => b.MeasuredAt)
                .Take(count)
                .ToListAsync();

        public async Task<Dictionary<string, double>> GetAveragesByTypeAsync()
            => await _db.PerformanceBenchmarks
                .GroupBy(b => b.OperationType)
                .Select(g => new { g.Key, Avg = g.Average(x => x.DurationMs) })
                .ToDictionaryAsync(x => x.Key, x => x.Avg);
    }
}
