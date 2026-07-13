namespace RAGChatBot.Domain.Interfaces
{
    public interface ICourseEventService
    {
        Task NotifyCourseChangedAsync(string changeType, Guid? courseId = null, string? courseCode = null,
            CancellationToken cancellationToken = default);
    }
}
