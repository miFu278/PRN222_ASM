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
            PayOSCallbackResult callbackResult,
            Guid userId)
        {
            if (!callbackResult.IsValid)
            {
                return false;
            }

            if (!callbackResult.IsSuccess)
            {
                await _transactionRepository.MarkFailedAsync(callbackResult.OrderId, userId);
                return false;
            }

            return await _transactionRepository.CompletePaymentAsync(
                callbackResult.OrderId,
                callbackResult.Amount,
                callbackResult.TransactionNo,
                userId);
        }

        public Task<bool> ProcessVerifiedPaymentAsync(
            string orderId,
            long amount,
            string? transactionNo)
            => _transactionRepository.CompletePaymentAsync(orderId, amount, transactionNo);

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

        public async Task CancelTransactionAsync(string orderId, Guid userId)
        {
            await _transactionRepository.MarkFailedAsync(orderId, userId);
        }
    }
}
