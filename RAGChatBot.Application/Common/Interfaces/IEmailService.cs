using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string toEmail, string? fullName);
    }
}
