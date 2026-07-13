using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RAGChatBot.Presentation.Hubs
{
    [Authorize]
    public class CourseHub : Hub
    {
    }
}
