using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public interface IPayOSService
    {
        Task<string> CreatePaymentUrl(long orderCode, long amount);
        Task<VnPayCallbackResult> ValidateReturnAsync(IReadOnlyDictionary<string, string> parameters);
    }
}
