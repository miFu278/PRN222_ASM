using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using RAGChatBot.DAL.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public class CreditService : ICreditService
    {
        private readonly IUserRepository _userRepository;
        private static readonly ConcurrentDictionary<Guid, int> _userCredits = new();

        public CreditService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<(bool allowed, int remaining)> CheckAndDeductCreditAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && user.SubscriptionTier == "Premium")
            {
                return (true, 9999);
            }

            // Đối với tài khoản Free, giới hạn là 10 lượt hỏi mỗi ngày
            var currentCredit = _userCredits.GetOrAdd(userId, 10);
            if (currentCredit <= 0)
            {
                return (false, 0);
            }

            var nextCredit = currentCredit - 1;
            _userCredits[userId] = nextCredit;
            return (true, nextCredit);
        }

        public async Task ResetDailyCreditsForFreeStudentsAsync()
        {
            _userCredits.Clear();
            await Task.CompletedTask;
        }
    }
}
