using System;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public interface ICreditService
    {
        Task<(bool allowed, int remaining)> CheckAndDeductCreditAsync(Guid userId);
        Task ResetDailyCreditsForFreeStudentsAsync();
    }
}
