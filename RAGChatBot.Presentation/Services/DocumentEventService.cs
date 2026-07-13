using Microsoft.AspNetCore.SignalR;
using RAGChatBot.Presentation.Hubs;
using RAGChatBot.Domain.Models;
using System;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Services
{
    public class DocumentEventService : RAGChatBot.Domain.Interfaces.IDocumentEventService
    {
        private readonly IHubContext<DocumentHub> _hubContext;
        private readonly ILogger<DocumentEventService> _logger;
        
        public DocumentEventService(IHubContext<DocumentHub> hubContext, ILogger<DocumentEventService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyDocumentChangedAsync(RealtimeChangeEvent change, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(change.CourseCode)) return;
            try
            {
                await _hubContext.Clients
                    .Group(RealtimeGroupNames.ForCourse(change.CourseCode))
                    .SendAsync("DocumentChanged", change, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Không thể phát DocumentChanged cho môn {CourseCode}", change.CourseCode);
            }
        }
    }
}

