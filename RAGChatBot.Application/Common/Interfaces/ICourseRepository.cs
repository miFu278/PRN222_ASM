using RAGChatBot.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface ICourseRepository
    {
        Task<Course> AddAsync(Course course);
        Task<IEnumerable<Course>> GetAllAsync();
        Task<IEnumerable<Course>> SearchAsync(string keyword);
        Task<Course?> GetByIdAsync(System.Guid id);
        Task UpdateAsync(Course course);
        Task DeleteAsync(Course course);
        Task<IEnumerable<Course>> GetBySubjectLeaderIdAsync(System.Guid userId);
    }
}
