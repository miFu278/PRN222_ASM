using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Hubs
{
    [Authorize]
    public class DocumentHub : Hub
    {
        public async Task JoinCourseGroup(string courseCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroupNames.ForCourse(courseCode));
        }

        public async Task LeaveCourseGroup(string courseCode)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroupNames.ForCourse(courseCode));
        }
    }
}
