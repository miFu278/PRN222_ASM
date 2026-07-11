namespace RAGChatBot.Domain.Interfaces
{
    public interface IDocumentEventService
    {
        void NotifyDocumentChanged(string courseCode);
    }
}
