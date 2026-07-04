using RAGChatBot.BLL.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RAGChatBot.BLL.Services
{
    public interface IWhitelistService
    {
        Task<bool> IsEmailWhitelistedAsync(string email);
        Task<IEnumerable<WhitelistEmailDto>> GetAllAsync();
        Task AddAsync(string email, string? fullName, string? studentId);
        Task DeleteAsync(Guid id);
        Task<int> ImportFromExcelAsync(Stream excelStream);
    }
}
