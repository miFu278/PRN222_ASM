using Microsoft.EntityFrameworkCore;
using RAGChatBot.DAL.Context;
using RAGChatBot.Domain.Entities;
using RAGChatBot.Domain.Interfaces;

namespace RAGChatBot.DAL.Repositories
{
    public sealed class RoleRepository : IRoleRepository
    {
        private readonly AppDbContext _db;

        public RoleRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<Role?> GetByNameAsync(string name)
            => _db.Roles.FirstOrDefaultAsync(role => role.Name.ToLower() == name.Trim().ToLower());

        public async Task<IReadOnlyList<Role>> GetAllAsync()
            => await _db.Roles
                .AsNoTracking()
                .OrderBy(role => role.Name)
                .ToListAsync();
    }
}
