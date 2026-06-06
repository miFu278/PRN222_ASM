using MiniExcelLibs;
using Microsoft.Extensions.Logging;
using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Domain.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Services
{
    public class WhitelistService : IWhitelistService
    {
        private readonly IWhitelistRepository _whitelistRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<WhitelistService> _logger;

        public WhitelistService(
            IWhitelistRepository whitelistRepository,
            IEmailService emailService,
            ILogger<WhitelistService> logger)
        {
            _whitelistRepository = whitelistRepository;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<bool> IsEmailWhitelistedAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var cleanEmail = email.Trim().ToLower();
            var whitelistEmail = await _whitelistRepository.GetByEmailAsync(cleanEmail);
            return whitelistEmail != null;
        }

        public async Task<IEnumerable<WhitelistEmailDto>> GetAllAsync()
        {
            var list = await _whitelistRepository.GetAllAsync();
            return list.Select(w => new WhitelistEmailDto
            {
                Id = w.Id,
                Email = w.Email,
                FullName = w.FullName,
                StudentId = w.StudentId,
                CreatedAt = w.CreatedAt
            }).OrderByDescending(w => w.CreatedAt);
        }

        public async Task AddAsync(string email, string? fullName, string? studentId)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email không được để trống!");
            }

            var cleanEmail = email.Trim().ToLower();
            var existing = await _whitelistRepository.GetByEmailAsync(cleanEmail);
            if (existing != null)
            {
                throw new Exception("Email này đã có trong danh sách Whitelist!");
            }

            var whitelistEmail = new WhitelistEmail
            {
                Id = Guid.NewGuid(),
                Email = cleanEmail,
                FullName = fullName?.Trim(),
                StudentId = studentId?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _whitelistRepository.AddAsync(whitelistEmail);
            await _whitelistRepository.SaveChangesAsync();

            try
            {
                await _emailService.SendWelcomeEmailAsync(cleanEmail, whitelistEmail.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi email chào mừng cho email {Email} vừa thêm vào Whitelist", cleanEmail);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _whitelistRepository.GetByIdAsync(id);
            if (entity == null)
            {
                throw new KeyNotFoundException("Không tìm thấy email cần xóa khỏi danh sách Whitelist!");
            }

            await _whitelistRepository.DeleteAsync(entity);
            await _whitelistRepository.SaveChangesAsync();
        }

        public async Task<int> ImportFromExcelAsync(Stream excelStream)
        {
            // MiniExcel đọc file thành dynamic rows (dạng IDictionary<string, object> khi useHeaderRow = true)
            var rows = excelStream.Query(useHeaderRow: true).Cast<IDictionary<string, object>>().ToList();
            if (!rows.Any()) return 0;

            // Lấy danh sách headers hiện tại của Excel
            var firstRow = rows.First();
            var headers = firstRow.Keys.ToList();

            // Tìm xem cột nào là email, họ tên, mssv dựa trên từ khóa tương ứng (đỡ bắt buộc nhập đúng tên cột tiếng Anh)
            var emailHeader = headers.FirstOrDefault(h => IsMatch(h, "email", "mail", "thư điện tử"));
            var fullNameHeader = headers.FirstOrDefault(h => IsMatch(h, "name", "tên", "họ và tên", "họ tên", "sinh viên"));
            var studentIdHeader = headers.FirstOrDefault(h => IsMatch(h, "mssv", "mã", "id", "mã số", "mã sinh viên"));

            if (string.IsNullOrEmpty(emailHeader))
            {
                throw new Exception("Không tìm thấy cột Email trong file Excel! Vui lòng kiểm tra lại tiêu đề file Excel.");
            }

            int count = 0;
            var importedEmails = new List<(string Email, string? FullName)>();
            foreach (var row in rows)
            {
                var emailObj = row.ContainsKey(emailHeader) ? row[emailHeader] : null;
                if (emailObj == null) continue;

                var emailStr = emailObj.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(emailStr) || !emailStr.Contains("@")) continue;

                var cleanEmail = emailStr.ToLower();
                var existing = await _whitelistRepository.GetByEmailAsync(cleanEmail);
                if (existing == null)
                {
                    string? fullName = null;
                    if (!string.IsNullOrEmpty(fullNameHeader) && row.TryGetValue(fullNameHeader, out var nameObj))
                    {
                        fullName = nameObj?.ToString();
                    }

                    string? studentId = null;
                    if (!string.IsNullOrEmpty(studentIdHeader) && row.TryGetValue(studentIdHeader, out var idObj))
                    {
                        studentId = idObj?.ToString();
                    }

                    var whitelistEmail = new WhitelistEmail
                    {
                        Id = Guid.NewGuid(),
                        Email = cleanEmail,
                        FullName = fullName?.Trim(),
                        StudentId = studentId?.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _whitelistRepository.AddAsync(whitelistEmail);
                    importedEmails.Add((cleanEmail, whitelistEmail.FullName));
                    count++;
                }
            }

            if (count > 0)
            {
                await _whitelistRepository.SaveChangesAsync();

                // Gửi email chào mừng cho từng tài khoản mới được import
                foreach (var item in importedEmails)
                {
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(item.Email, item.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi gửi email chào mừng cho email {Email} trong quá trình import", item.Email);
                    }
                }
            }

            return count;
        }

        private bool IsMatch(string header, params string[] keywords)
        {
            var cleanHeader = header.Trim().ToLower();
            return keywords.Any(kw => cleanHeader.Contains(kw));
        }
    }
}
