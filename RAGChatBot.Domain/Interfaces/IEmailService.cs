namespace RAGChatBot.Domain.Interfaces
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string toEmail, string? fullName);
        Task SendUserAccountEmailAsync(string toEmail, string? fullName, string username, string password, string role);
    }
}
