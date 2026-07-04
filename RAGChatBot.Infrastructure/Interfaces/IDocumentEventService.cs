namespace RAGChatBot.Infrastructure.Interfaces
{
    public interface IDocumentEventService
    {
        void NotifyDocumentChanged(string courseCode);
    }
}
