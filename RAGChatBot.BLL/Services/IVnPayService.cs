using System;
using Microsoft.AspNetCore.Http;

namespace RAGChatBot.BLL.Services
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(Guid userId, string ipAddress);
        VnPayCallbackResult ValidateCallback(IQueryCollection query);
    }
}
