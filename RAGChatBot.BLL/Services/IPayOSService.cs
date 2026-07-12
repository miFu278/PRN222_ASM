using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public interface IPayOSService
    {
        Task<string> CreatePaymentUrl(long orderCode, long amount);
        VnPayCallbackResult ValidateCallback(IReadOnlyDictionary<string, string> parameters);
    }
}
