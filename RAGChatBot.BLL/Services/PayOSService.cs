using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace RAGChatBot.BLL.Services
{
    public class PayOSService : IPayOSService
    {
        private readonly PayOSClient _payOSClient;
        private readonly string _returnUrl;
        private readonly string _cancelUrl;

        public PayOSService(PayOSClient payOSClient, string returnUrl, string cancelUrl)
        {
            _payOSClient = payOSClient;
            _returnUrl = returnUrl;
            _cancelUrl = cancelUrl;
        }

        public async Task<string> CreatePaymentUrl(long orderCode, long amount)
        {
            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = amount,
                Description = $"DH {orderCode}".Length > 25
                    ? $"DH {orderCode}"[..25]
                    : $"DH {orderCode}",
                ReturnUrl = _returnUrl,
                CancelUrl = _cancelUrl
            };

            var response = await _payOSClient.PaymentRequests.CreateAsync(request);
            return response.CheckoutUrl;
        }

        public async Task<PayOSCallbackResult> ValidateReturnAsync(IReadOnlyDictionary<string, string> parameters)
        {
            parameters.TryGetValue("orderCode", out var orderCodeStr);
            parameters.TryGetValue("id", out var paymentLinkId);
            parameters.TryGetValue("code", out var responseCode);
            if (!long.TryParse(orderCodeStr, out var orderCode))
            {
                return new PayOSCallbackResult
                {
                    OrderId = orderCodeStr ?? string.Empty,
                    ResponseCode = responseCode ?? string.Empty,
                    IsValid = false,
                    Message = "Mã giao dịch PayOS không hợp lệ."
                };
            }

            try
            {
                // PayOS return URLs are not signed. Verify the transaction against PayOS itself.
                var paymentLink = await _payOSClient.PaymentRequests.GetAsync(orderCode);
                var isSuccess = paymentLink.Status == PaymentLinkStatus.Paid;
                return new PayOSCallbackResult
                {
                    OrderId = orderCodeStr!,
                    ResponseCode = responseCode ?? string.Empty,
                    IsValid = true,
                    IsSuccess = isSuccess,
                    Message = isSuccess ? "Giao dịch Premium thành công!" : "Giao dịch thất bại hoặc bị hủy.",
                    TransactionNo = paymentLinkId,
                    Amount = paymentLink.Amount
                };
            }
            catch
            {
                return new PayOSCallbackResult
                {
                    OrderId = orderCodeStr!,
                    ResponseCode = responseCode ?? string.Empty,
                    IsValid = false,
                    Message = "Không thể xác minh giao dịch với PayOS."
                };
            }
        }
    }
}
