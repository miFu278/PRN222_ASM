using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Interfaces
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
