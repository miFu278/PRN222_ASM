using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        private readonly string _checksumKey;

        public PayOSService(PayOSClient payOSClient, string returnUrl, string cancelUrl)
        {
            _payOSClient = payOSClient;
            _returnUrl = returnUrl;
            _cancelUrl = cancelUrl;
            _checksumKey = payOSClient.ChecksumKey;
        }

        public async Task<string> CreatePaymentUrl(long orderCode, long amount)
        {
            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = amount,
                Description = $"Thanh toan don hang {orderCode}",
                ReturnUrl = _returnUrl,
                CancelUrl = _cancelUrl
            };

            var response = await _payOSClient.PaymentRequests.CreateAsync(request);
            return response.CheckoutUrl;
        }

        public VnPayCallbackResult ValidateCallback(IReadOnlyDictionary<string, string> parameters)
        {
            bool isSignatureValid = VerifyRedirectSignature(parameters);

            parameters.TryGetValue("orderCode", out var orderCodeStr);
            parameters.TryGetValue("status", out var status);
            parameters.TryGetValue("id", out var paymentLinkId);
            parameters.TryGetValue("code", out var responseCode);

            bool isSuccess = isSignatureValid && string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) && string.Equals(responseCode, "00", StringComparison.OrdinalIgnoreCase);

            long amount = 0;
            if (isSignatureValid && long.TryParse(orderCodeStr, out var orderCode))
            {
                try
                {
                    // Fetch real payment link details from PayOS to verify the amount securely
                    var paymentLink = Task.Run(() => _payOSClient.PaymentRequests.GetAsync(orderCode)).GetAwaiter().GetResult();
                    if (paymentLink != null)
                    {
                        amount = paymentLink.Amount;
                        if (paymentLink.Status == PaymentLinkStatus.Paid)
                        {
                            isSuccess = true;
                        }
                    }
                }
                catch
                {
                    // Fallback if API call fails
                }
            }

            return new VnPayCallbackResult
            {
                OrderId = orderCodeStr ?? string.Empty,
                ResponseCode = responseCode ?? string.Empty,
                IsValid = isSignatureValid,
                IsSuccess = isSuccess,
                Message = isSuccess ? "Giao dịch Premium thành công!" : "Giao dịch thất bại hoặc bị hủy.",
                TransactionNo = paymentLinkId,
                Amount = amount
            };
        }

        private bool VerifyRedirectSignature(IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("signature", out var signature) || string.IsNullOrEmpty(signature))
            {
                return false;
            }

            // Exclude signature and sort keys alphabetically
            var sortedParams = parameters
                .Where(p => !string.Equals(p.Key, "signature", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={p.Value}");

            var rawData = string.Join("&", sortedParams);

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_checksumKey)))
            {
                var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                var computedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                return string.Equals(computedSignature, signature, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
