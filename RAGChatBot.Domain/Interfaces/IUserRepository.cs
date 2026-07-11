using RAGChatBot.Domain.Entities;

namespace RAGChatBot.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByUsernameAsync(string username);
        Task AddAsync(User user);
        Task<IEnumerable<User>> GetAllAsync();
        Task DeleteAsync(User user);
        Task SaveChangesAsync();
    }
}
