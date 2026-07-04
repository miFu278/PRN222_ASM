using RAGChatBot.Application.BusinessEntities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.Application.ServiceInterfaces
{
    public interface IAuthService
    {
        Task<UserDto?> LoginAsync(LoginRequest request);
        Task<UserDto> RegisterAsync(string username, string password, string role, string subscriptionTier, string fullName);
        Task<bool> UpgradeToPremiumAsync(Guid userId);
        Task<bool> ToggleSubscriptionTierAsync(Guid userId);
        Task<UserDto?> GetUserByUsernameAsync(string username);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task DeleteUserAsync(Guid userId);
        Task<(int Success, int Skipped)> ImportUsersFromExcelAsync(Stream excelStream, string defaultPassword);
    }
}
