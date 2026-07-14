namespace RAGChatBot.Domain.Interfaces
{
    public interface IChatSessionRepository
    {
        Task<(bool Allowed, int Remaining)> TryConsumeDailyCreditAsync(
            Guid userId,
            string courseCode,
            DateOnly usageDate,
            int dailyLimit);
        Task RefundDailyCreditAsync(Guid userId, DateOnly usageDate);
    }
}
