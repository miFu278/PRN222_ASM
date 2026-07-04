using Microsoft.EntityFrameworkCore;
using RAGChatBot.Infrastructure.Interfaces;
using RAGChatBot.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.DataAccess.Repositories
{
    public class WhitelistRepository : IWhitelistRepository
    {
        private readonly AppDbContext _context;

        public WhitelistRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<WhitelistEmail?> GetByIdAsync(Guid id)
        {
            return await _context.WhitelistEmails.FindAsync(id);
        }

        public async Task<WhitelistEmail?> GetByEmailAsync(string email)
        {
            return await _context.WhitelistEmails.FirstOrDefaultAsync(w => w.Email == email);
        }

        public async Task<IEnumerable<WhitelistEmail>> GetAllAsync()
        {
            return await _context.WhitelistEmails.ToListAsync();
        }

        public async Task AddAsync(WhitelistEmail entity)
        {
            await _context.WhitelistEmails.AddAsync(entity);
        }

        public async Task DeleteAsync(WhitelistEmail entity)
        {
            _context.WhitelistEmails.Remove(entity);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
