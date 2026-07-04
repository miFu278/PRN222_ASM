using RAGChatBot.Application.BusinessEntities;

namespace RAGChatBot.Application.ServiceInterfaces
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
