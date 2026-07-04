using RAGChatBot.Infrastructure.Models;

namespace RAGChatBot.Infrastructure.Interfaces
{
    public interface IWhitelistRepository
    {
        Task<WhitelistEmail?> GetByIdAsync(Guid id);
        Task<WhitelistEmail?> GetByEmailAsync(string email);
        Task<IEnumerable<WhitelistEmail>> GetAllAsync();
        Task AddAsync(WhitelistEmail entity);
        Task DeleteAsync(WhitelistEmail entity);
        Task SaveChangesAsync();
    }
}
