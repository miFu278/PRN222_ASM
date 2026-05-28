using RAGChatBot.Domain.Models;
using System;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Common.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByUsernameAsync(string username);
        Task AddAsync(User user);
        Task SaveChangesAsync();
    }
}
