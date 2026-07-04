using Microsoft.AspNetCore.SignalR;
using RAGChatBot.Presentation.Hubs;
using System;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Services
{
    public class DocumentEventService : RAGChatBot.Infrastructure.Interfaces.IDocumentEventService
    {
        private readonly IHubContext<DocumentHub> _hubContext;
        
        public event Action<string>? OnDocumentChanged;

        public DocumentEventService(IHubContext<DocumentHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void NotifyDocumentChanged(string courseCode)
        {
            OnDocumentChanged?.Invoke(courseCode);
            // Kích hoạt SignalR event cho client đang xem môn học này
            _hubContext.Clients.Group(courseCode.ToLowerInvariant()).SendAsync("DocumentChanged", courseCode).GetAwaiter().GetResult();
        }
    }
}

