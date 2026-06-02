using RAGChatBot.Application.DTOs;

namespace RAGChatBot.Application.Services
{
    public interface ICourseService
    {
        Task<IEnumerable<CourseDto>> GetAllCoursesAsync();
        Task<IEnumerable<CourseDto>> SearchCoursesAsync(string keyword);
        Task<CourseDto> CreateCourseAsync(CourseDto courseDto, Guid userId);
    }
}
