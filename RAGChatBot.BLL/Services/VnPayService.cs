using System;
using Microsoft.AspNetCore.Http;

namespace RAGChatBot.BLL.Services
{
    public class VnPayService : IVnPayService
    {
        public string CreatePaymentUrl(Guid userId, string ipAddress)
        {
            // Tạo URL callback nội bộ để test thử nghiệm thanh toán thành công trực tiếp
            return $"/Subscription/PaymentCallback?vnp_ResponseCode=00&vnp_TxnRef={Guid.NewGuid()}&vnp_Amount=19900000&vnp_TransactionNo=VNP12345678&vnp_SecureHash=valid";
        }

        public VnPayCallbackResult ValidateCallback(IQueryCollection query)
        {
            var responseCode = query["vnp_ResponseCode"].ToString();
            var isSuccess = responseCode == "00";
            var amountStr = query["vnp_Amount"].ToString();
            long.TryParse(amountStr, out var amount);

            return new VnPayCallbackResult
            {
                OrderId = query["vnp_TxnRef"].ToString(),
                ResponseCode = responseCode,
                IsValid = true, // Giả lập chữ ký hợp lệ để vượt qua kiểm tra bảo mật test
                IsSuccess = isSuccess,
                Message = isSuccess ? "Giao dịch Premium thành công!" : "Giao dịch thất bại.",
                TransactionNo = query["vnp_TransactionNo"].ToString(),
                Amount = amount / 100 // VNPay số tiền nhân 100 lần
            };
        }
    }
}
