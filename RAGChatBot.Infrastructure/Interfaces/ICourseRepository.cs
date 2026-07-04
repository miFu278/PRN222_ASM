using RAGChatBot.Infrastructure.Models;

namespace RAGChatBot.Infrastructure.Interfaces
{
    public interface ICourseRepository
    {
        Task<Course> AddAsync(Course course);
        Task<IEnumerable<Course>> GetAllAsync();
        Task<IEnumerable<Course>> SearchAsync(string keyword);
        Task<Course?> GetByIdAsync(Guid id);
        Task UpdateAsync(Course course);
        Task DeleteAsync(Course course);
        Task<IEnumerable<Course>> GetBySubjectLeaderIdAsync(Guid userId);
    }
}
