namespace RAGChatBot.DAL.Interfaces
{
    public interface IChatSessionRepository
    {
        Task<int> CountByMonthAsync(int year, int month);
        Task<int> CountByQuarterAsync(int year, int quarter);
        Task<int> CountByYearAsync(int year);
        Task IncrementAsync(Guid userId, string courseCode);
    }
}
