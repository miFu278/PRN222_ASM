using Microsoft.AspNetCore.SignalR;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Presentation.Hubs;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Services
{
    public class CourseEventService : ICourseEventService
    {
        private readonly IHubContext<CourseHub> _hubContext;

        public CourseEventService(IHubContext<CourseHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void NotifyCourseChanged()
        {
            _hubContext.Clients.All.SendAsync("CourseChanged").GetAwaiter().GetResult();
        }
    }
}
