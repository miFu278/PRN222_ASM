namespace RAGChatBot.BLL.Services
{
    public class VnPayCallbackResult
    {
        public string OrderId { get; set; } = string.Empty;
        public string ResponseCode { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TransactionNo { get; set; }
        public long Amount { get; set; }
    }
}
