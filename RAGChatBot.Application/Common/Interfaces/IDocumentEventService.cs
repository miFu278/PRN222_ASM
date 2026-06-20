namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IDocumentEventService
    {
        void NotifyDocumentChanged(string courseCode);
    }
}
