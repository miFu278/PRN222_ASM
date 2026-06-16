namespace RAGChatBot.Blazor.Services
{
    public class DocumentEventService
    {
        public event Action<string>? OnDocumentChanged;

        public void NotifyDocumentChanged(string courseCode)
        {
            OnDocumentChanged?.Invoke(courseCode);
        }
    }
}
