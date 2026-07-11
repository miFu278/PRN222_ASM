namespace RAGChatBot.BLL.Services
{
    public class VnPayService : IVnPayService
    {
        public string CreatePaymentUrl(Guid userId, string ipAddress, string orderId)
        {
            // Callback nội bộ dùng cho luồng thanh toán thử nghiệm hiện tại.
            return $"/Subscription/PaymentCallback?vnp_ResponseCode=00&vnp_TxnRef={Uri.EscapeDataString(orderId)}&vnp_Amount=19900000&vnp_TransactionNo=VNP12345678&vnp_SecureHash=valid";
        }

        public VnPayCallbackResult ValidateCallback(IReadOnlyDictionary<string, string> parameters)
        {
            var responseCode = GetValue(parameters, "vnp_ResponseCode");
            var isSuccess = responseCode == "00";
            long.TryParse(GetValue(parameters, "vnp_Amount"), out var amount);

            return new VnPayCallbackResult
            {
                OrderId = GetValue(parameters, "vnp_TxnRef"),
                ResponseCode = responseCode,
                IsValid = true,
                IsSuccess = isSuccess,
                Message = isSuccess ? "Giao dịch Premium thành công!" : "Giao dịch thất bại.",
                TransactionNo = GetValue(parameters, "vnp_TransactionNo"),
                Amount = amount / 100
            };
        }

        private static string GetValue(
            IReadOnlyDictionary<string, string> parameters,
            string key)
            => parameters.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
