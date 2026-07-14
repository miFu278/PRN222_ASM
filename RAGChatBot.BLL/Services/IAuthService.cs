using RAGChatBot.BLL.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public interface IAuthService
    {
        Task<UserDto?> LoginAsync(LoginRequest request);
        Task<UserDto> RegisterAsync(string username, string password, string role, string subscriptionTier, string fullName);
        Task<bool> ToggleSubscriptionTierAsync(Guid userId);
        Task<UserDto?> GetUserByIdAsync(Guid id);
        Task<UserDto?> GetUserByUsernameAsync(string username);
        Task<IEnumerable<UserDto>> GetAllUsersAsync();
        Task DeleteUserAsync(Guid userId);
        Task<(int Success, int Skipped)> ImportUsersFromExcelAsync(Stream excelStream, string defaultPassword);
    }
}
