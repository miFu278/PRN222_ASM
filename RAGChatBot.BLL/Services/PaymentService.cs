using RAGChatBot.BLL.DTOs;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPaymentTransactionRepository _transactionRepository;

        public PaymentService(
            IUserRepository userRepository,
            IPaymentTransactionRepository transactionRepository)
        {
            _userRepository = userRepository;
            _transactionRepository = transactionRepository;
        }

        public async Task<string> CreatePendingTransactionAsync(Guid userId, long amount, string? orderId = null)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User was not found.");

            orderId ??= $"ORDER-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
            await _transactionRepository.AddAsync(new PaymentTransaction
            {
                OrderId = orderId,
                UserId = user.Id,
                Amount = amount,
                Status = "Pending"
            });
            await _transactionRepository.SaveChangesAsync();

            return orderId;
        }

        public async Task<bool> ProcessPaymentCallbackAsync(
            VnPayCallbackResult callbackResult,
            Guid userId)
        {
            if (!callbackResult.IsValid)
            {
                return false;
            }

            var transaction = await _transactionRepository.GetByOrderIdAsync(callbackResult.OrderId);
            if (transaction == null || transaction.UserId != userId)
            {
                return false;
            }

            // A repeated successful callback must not extend the subscription twice.
            if (transaction.Status == "Success")
            {
                return true;
            }

            transaction.TransactionNo = callbackResult.TransactionNo;
            var isSuccessfulPayment = callbackResult.IsSuccess && callbackResult.Amount == transaction.Amount;
            transaction.Status = isSuccessfulPayment ? "Success" : "Failed";
            transaction.PaidAt = isSuccessfulPayment ? DateTime.UtcNow : null;

            if (isSuccessfulPayment)
            {
                var user = transaction.User;
                user.SubscriptionTier = "Premium";
                var subscriptionStart = user.SubscriptionExpiresAt > DateTime.UtcNow
                    ? user.SubscriptionExpiresAt.Value
                    : DateTime.UtcNow;
                user.SubscriptionExpiresAt = subscriptionStart.AddMonths(1);
            }

            await _transactionRepository.SaveChangesAsync();
            return isSuccessfulPayment;
        }

        public async Task<IEnumerable<PaymentTransactionDto>> GetAllTransactionsAsync()
        {
            var transactions = await _transactionRepository.GetAllAsync();
            return transactions.Select(transaction => new PaymentTransactionDto
            {
                OrderId = transaction.OrderId,
                FullName = transaction.User.FullName,
                Username = transaction.User.Username,
                Amount = transaction.Amount,
                TransactionNo = transaction.TransactionNo,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt,
                PaidAt = transaction.PaidAt
            }).ToList();
        }

        public async Task CancelTransactionAsync(string orderId)
        {
            var transaction = await _transactionRepository.GetByOrderIdAsync(orderId);
            if (transaction != null && transaction.Status == "Pending")
            {
                transaction.Status = "Failed";
                await _transactionRepository.SaveChangesAsync();
            }
        }
    }
}
