using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RAGChatBot.BLL.DTOs;
using RAGChatBot.DAL.Interfaces;

namespace RAGChatBot.BLL.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUserRepository _userRepository;

        public PaymentService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> ProcessPaymentCallbackAsync(VnPayCallbackResult callbackResult, Guid userId)
        {
            if (callbackResult.IsSuccess && callbackResult.IsValid)
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null)
                {
                    user.SubscriptionTier = "Premium";
                    await _userRepository.SaveChangesAsync();
                    return true;
                }
            }
            return false;
        }

        public async Task<IEnumerable<PaymentTransactionDto>> GetAllTransactionsAsync()
        {
            var list = new List<PaymentTransactionDto>
            {
                new PaymentTransactionDto
                {
                    OrderId = "ORDER-TXN-101",
                    FullName = "Nguyễn Văn A",
                    Username = "sv_studentA",
                    Amount = 199000,
                    TransactionNo = "VNP87654321",
                    Status = "Success",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    PaidAt = DateTime.UtcNow.AddDays(-2).AddMinutes(5)
                },
                new PaymentTransactionDto
                {
                    OrderId = "ORDER-TXN-102",
                    FullName = "Trần Thị B",
                    Username = "sv_studentB",
                    Amount = 199000,
                    TransactionNo = null,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    PaidAt = null
                },
                new PaymentTransactionDto
                {
                    OrderId = "ORDER-TXN-103",
                    FullName = "Lê Văn C",
                    Username = "sv_studentC",
                    Amount = 199000,
                    TransactionNo = "VNP87654323",
                    Status = "Failed",
                    CreatedAt = DateTime.UtcNow,
                    PaidAt = null
                }
            };
            return await Task.FromResult(list);
        }
    }
}
