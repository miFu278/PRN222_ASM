namespace RAGChatBot.DAL.Interfaces
{
    public interface IDocumentEventService
    {
        void NotifyDocumentChanged(string courseCode);
    }
}
