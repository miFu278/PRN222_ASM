using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RAGChatBot.BLL.Services;
using RAGChatBot.Domain.Constants;
using System.Security.Claims;

namespace RAGChatBot.Presentation.Hubs
{
    [Authorize]
    public sealed class QuizHub : Hub
    {
        private readonly ICourseService _courseService;

        public QuizHub(ICourseService courseService)
        {
            _courseService = courseService;
        }

        public async Task JoinCourseGroup(string courseCode)
        {
            var normalizedCode = await EnsureCourseAccessAsync(courseCode);
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroupNames.ForCourse(normalizedCode));
        }

        public Task LeaveCourseGroup(string courseCode)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroupNames.ForCourse(courseCode));

        private async Task<string> EnsureCourseAccessAsync(string courseCode)
        {
            var normalizedCode = courseCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedCode) ||
                !Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                throw new HubException("Phiên người dùng hoặc mã môn học không hợp lệ.");
            }

            var courseExists = (await _courseService.GetAllCoursesAsync())
                .Any(course => course.Code.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
            var allowed = courseExists &&
                (Context.User!.IsInRole(RoleNames.Admin) ||
                 Context.User.IsInRole(RoleNames.Student) ||
                 (Context.User.IsInRole(RoleNames.Lecturer) &&
                  await _courseService.IsSubjectLeaderAsync(normalizedCode, userId)));
            if (!allowed) throw new HubException("Bạn không có quyền theo dõi môn học này.");
            return normalizedCode;
        }
    }
}
