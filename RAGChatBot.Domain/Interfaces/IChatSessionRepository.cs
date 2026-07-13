namespace RAGChatBot.Domain.Interfaces
{
    public interface IChatSessionRepository
    {
        Task<int> CountByMonthAsync(int year, int month);
        Task<int> CountByQuarterAsync(int year, int quarter);
        Task<int> CountByYearAsync(int year);
        Task<int> GetTodayMessageCountAsync(Guid userId);
        Task IncrementAsync(Guid userId, string courseCode);
    }
}
