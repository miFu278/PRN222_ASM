using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace RAGChatBot.BLL.Services
{
    public class PayOSService : IPayOSService
    {
        private readonly PayOSClient _client;
        private readonly string _returnUrl;
        private readonly string _cancelUrl;

        public PayOSService(PayOSClient client, string returnUrl, string cancelUrl)
        {
            _client = client;
            _returnUrl = returnUrl;
            _cancelUrl = cancelUrl;
        }

        public async Task<string> CreatePaymentUrl(long orderCode, long amount)
        {
            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (int)amount,
                Description = "Premium " + orderCode,
                ReturnUrl = _returnUrl,
                CancelUrl = _cancelUrl
            };

            var result = await _client.PaymentRequests.CreateAsync(request);
            return result.CheckoutUrl;
        }

        public PayOSCallbackResult ValidateCallback(IReadOnlyDictionary<string, string> parameters)
        {
            var code = GetValue(parameters, "code");
            var status = GetValue(parameters, "status");
            var orderCodeStr = GetValue(parameters, "orderCode");
            var idStr = GetValue(parameters, "id");
            var cancel = GetValue(parameters, "cancel");

            var isSuccess = code == "00" && status == "PAID" && cancel != "true";

            return new PayOSCallbackResult
            {
                OrderId = orderCodeStr,
                ResponseCode = code,
                IsValid = true,
                IsSuccess = isSuccess,
                Message = isSuccess ? "Giao dịch Premium thành công!" : "Giao dịch thất bại.",
                TransactionNo = idStr,
                Amount = 0 // Sẽ được so khớp từ transaction trong DB
            };
        }

        private static string GetValue(
            IReadOnlyDictionary<string, string> parameters,
            string key)
            => parameters.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
