using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RAGChatBot.Presentation.Hubs
{
    [Authorize]
    public sealed class QuizHub : Hub
    {
        public Task JoinCourseGroup(string courseCode)
            => Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroupNames.ForCourse(courseCode));

        public Task LeaveCourseGroup(string courseCode)
            => Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroupNames.ForCourse(courseCode));
    }
}
