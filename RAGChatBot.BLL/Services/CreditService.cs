using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public class CreditService : ICreditService
    {
        private readonly int _freeDailyLimit;
        private readonly int _premiumDailyLimit;
        private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();
        private readonly IUserRepository _userRepository;
        private readonly IChatSessionRepository _chatSessionRepository;

        public CreditService(
            IUserRepository userRepository,
            IChatSessionRepository chatSessionRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _chatSessionRepository = chatSessionRepository;
            _freeDailyLimit = configuration.GetValue<int>("SubscriptionSettings:FreeDailyLimit", 10);
            _premiumDailyLimit = configuration.GetValue<int>("SubscriptionSettings:PremiumDailyLimit", 50);
        }

        public async Task<(bool allowed, int remaining)> CheckAndDeductCreditAsync(
            Guid userId,
            string courseCode)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            var dailyLimit = user != null &&
                string.Equals(user.SubscriptionTier, "Premium", StringComparison.OrdinalIgnoreCase) &&
                (!user.SubscriptionExpiresAt.HasValue || user.SubscriptionExpiresAt.Value > DateTime.UtcNow)
                    ? _premiumDailyLimit
                    : _freeDailyLimit;

            return await _chatSessionRepository.TryConsumeDailyCreditAsync(
                userId,
                courseCode,
                GetVietnamDate(),
                dailyLimit);
        }

        public Task RefundCreditAsync(Guid userId)
            => _chatSessionRepository.RefundDailyCreditAsync(userId, GetVietnamDate());

        private static DateOnly GetVietnamDate()
            => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }
    }
}
