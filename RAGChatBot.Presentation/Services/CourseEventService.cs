using Microsoft.AspNetCore.SignalR;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Presentation.Hubs;
using RAGChatBot.Domain.Models;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Services
{
    public class CourseEventService : ICourseEventService
    {
        private readonly IHubContext<CourseHub> _hubContext;
        private readonly ILogger<CourseEventService> _logger;

        public CourseEventService(IHubContext<CourseHub> hubContext, ILogger<CourseEventService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyCourseChangedAsync(string changeType, Guid? courseId = null, string? courseCode = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("CourseChanged", new RealtimeChangeEvent
                {
                    Type = changeType,
                    CourseCode = courseCode,
                    EntityId = courseId
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Không thể phát CourseChanged");
            }
        }
    }
}
