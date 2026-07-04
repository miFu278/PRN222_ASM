using RAGChatBot.BLL.DTOs;

namespace RAGChatBot.BLL.Services
{
    public interface ICourseService
    {
        Task<IEnumerable<CourseDto>> GetAllCoursesAsync();
        Task<IEnumerable<CourseDto>> SearchCoursesAsync(string keyword);
        Task<CourseDto> CreateCourseAsync(CourseDto courseDto, Guid userId);
        Task<bool> IsSubjectLeaderAsync(string courseCode, Guid userId);
        Task UpdateSubjectLeaderAsync(Guid courseId, Guid subjectLeaderId);
        Task UpdateCourseAsync(CourseDto courseDto);
        Task DeleteCourseAsync(Guid id);
        Task<IEnumerable<CourseDto>> GetCoursesBySubjectLeaderAsync(Guid userId);
    }
}
