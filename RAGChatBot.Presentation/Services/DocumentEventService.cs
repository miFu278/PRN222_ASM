namespace RAGChatBot.Presentation.Services
{
    public class DocumentEventService : RAGChatBot.Application.Common.Interfaces.IDocumentEventService
    {
        public event Action<string>? OnDocumentChanged;

        public void NotifyDocumentChanged(string courseCode)
        {
            OnDocumentChanged?.Invoke(courseCode);
        }
    }
}

