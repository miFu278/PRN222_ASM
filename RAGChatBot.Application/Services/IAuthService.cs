using RAGChatBot.Application.DTOs;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Services
{
    public interface IAuthService
    {
        Task<UserDto?> LoginAsync(LoginRequest request);
        Task<UserDto> RegisterAsync(string username, string password, string role, string subscriptionTier);
        Task<bool> UpgradeToPremiumAsync(Guid userId);
        Task<UserDto?> GetUserByUsernameAsync(string username);
    }
}
