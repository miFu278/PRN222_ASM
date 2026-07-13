using System;
using System.Threading.Tasks;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public class CreditService : ICreditService
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatSessionRepository _chatSessionRepository;

        public CreditService(
            IUserRepository userRepository,
            IChatSessionRepository chatSessionRepository)
        {
            _userRepository = userRepository;
            _chatSessionRepository = chatSessionRepository;
        }

        public async Task<(bool allowed, int remaining)> CheckAndDeductCreditAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null &&
                string.Equals(user.SubscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase) &&
                (!user.SubscriptionExpiresAt.HasValue || user.SubscriptionExpiresAt.Value > DateTime.UtcNow))
            {
                return (true, 9999);
            }

            // Usage is persisted and only incremented after a successful RAG response.
            var usedToday = await _chatSessionRepository.GetTodayMessageCountAsync(userId);
            var remaining = Math.Max(0, 10 - usedToday);
            return remaining > 0
                ? (true, remaining - 1)
                : (false, 0);
        }

        public async Task ResetDailyCreditsForFreeStudentsAsync()
        {
            await Task.CompletedTask;
        }
    }
}
