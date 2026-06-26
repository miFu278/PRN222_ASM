using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Hubs
{
    public class DocumentHub : Hub
    {
        public async Task JoinCourseGroup(string courseCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, courseCode.ToLowerInvariant());
        }

        public async Task LeaveCourseGroup(string courseCode)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, courseCode.ToLowerInvariant());
        }
    }
}
