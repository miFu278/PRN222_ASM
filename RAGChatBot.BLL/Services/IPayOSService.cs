namespace RAGChatBot.BLL.Services
{
    public interface IPayOSService
    {
        Task<string> CreatePaymentUrl(long orderCode, long amount);
        PayOSCallbackResult ValidateCallback(IReadOnlyDictionary<string, string> parameters);
    }
}
