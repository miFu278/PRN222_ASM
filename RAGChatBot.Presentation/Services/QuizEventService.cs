using Microsoft.AspNetCore.SignalR;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.Domain.Models;
using RAGChatBot.Presentation.Hubs;

namespace RAGChatBot.Presentation.Services
{
    public sealed class QuizEventService : IQuizEventService
    {
        private readonly IHubContext<QuizHub> _hubContext;
        private readonly ILogger<QuizEventService> _logger;

        public QuizEventService(IHubContext<QuizHub> hubContext, ILogger<QuizEventService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyQuizChangedAsync(RealtimeChangeEvent change, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(change.CourseCode)) return;
            try
            {
                await _hubContext.Clients
                    .Group(RealtimeGroupNames.ForCourse(change.CourseCode))
                    .SendAsync("QuizChanged", change, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Không thể phát QuizChanged cho môn {CourseCode}", change.CourseCode);
            }
        }
    }
}
