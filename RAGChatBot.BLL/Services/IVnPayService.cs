namespace RAGChatBot.BLL.Services
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(Guid userId, string ipAddress, string orderId);
        VnPayCallbackResult ValidateCallback(IReadOnlyDictionary<string, string> parameters);
    }
}
