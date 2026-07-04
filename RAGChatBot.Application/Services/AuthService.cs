using MiniExcelLibs;
using RAGChatBot.Infrastructure.Interfaces;
using RAGChatBot.Application.DTOs;
using RAGChatBot.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RAGChatBot.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailService _emailService;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, IEmailService emailService)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        public async Task<UserDto?> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user == null) return null;

            var isValid = _passwordHasher.Verify(request.Password, user.PasswordHash);
            if (!isValid) return null;

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            };
        }

        public async Task<UserDto> RegisterAsync(string username, string password, string role, string subscriptionTier, string fullName)
        {
            var existingUser = await _userRepository.GetByUsernameAsync(username);
            if (existingUser != null)
            {
                throw new Exception("Tên tài khoản này đã tồn tại trong hệ thống!");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = _passwordHasher.Hash(password),
                Role = role,
                SubscriptionTier = subscriptionTier,
                FullName = fullName.Trim()
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            };
        }

        public async Task<bool> UpgradeToPremiumAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.SubscriptionTier = "Premium";
            await _userRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleSubscriptionTierAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            user.SubscriptionTier = user.SubscriptionTier == "Premium" ? "Free" : "Premium";
            await _userRepository.SaveChangesAsync();
            return true;
        }

        public async Task<UserDto?> GetUserByUsernameAsync(string username)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null) return null;
            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            };
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(user => new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                SubscriptionTier = user.SubscriptionTier,
                FullName = user.FullName
            }).OrderBy(u => u.Role).ThenBy(u => u.Username);
        }

        public async Task DeleteUserAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài khoản người dùng cần xóa!");
            }

            // Bảo mật: Không cho phép tự xóa tài khoản Admin để tránh mất quyền quản trị
            if (user.Role == "Admin")
            {
                throw new InvalidOperationException("Không được phép xóa tài khoản quản trị hệ thống (Admin)!");
            }

            await _userRepository.DeleteAsync(user);
            await _userRepository.SaveChangesAsync();
        }

        public async Task<(int Success, int Skipped)> ImportUsersFromExcelAsync(Stream excelStream, string defaultPassword)
        {
            var rows = excelStream.Query(useHeaderRow: true).Cast<IDictionary<string, object>>().ToList();
            if (!rows.Any()) return (0, 0);

            var headers = rows.First().Keys.ToList();

            var emailHeader = headers.FirstOrDefault(h => IsMatch(h, "email", "mail", "username", "tài khoản"));
            var fullNameHeader = headers.FirstOrDefault(h => IsMatch(h, "name", "tên", "họ và tên", "họ tên", "fullname"));
            var roleHeader = headers.FirstOrDefault(h => IsMatch(h, "role", "vai trò", "chức vụ", "loại"));
            var tierHeader = headers.FirstOrDefault(h => IsMatch(h, "tier", "gói", "subscription", "premium"));
            var passwordHeader = headers.FirstOrDefault(h => IsMatch(h, "password", "mật khẩu", "pass"));

            if (string.IsNullOrEmpty(emailHeader))
                throw new Exception("Không tìm thấy cột Email/Username trong file Excel!");

            int success = 0, skipped = 0;
            var newUsers = new List<(string Email, string FullName, string Password, string Role)>();

            foreach (var row in rows)
            {
                var emailObj = row.ContainsKey(emailHeader) ? row[emailHeader] : null;
                if (emailObj == null) continue;
                var username = emailObj.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(username)) continue;

                var existing = await _userRepository.GetByUsernameAsync(username);
                if (existing != null) { skipped++; continue; }

                string? fullName = null;
                if (!string.IsNullOrEmpty(fullNameHeader) && row.TryGetValue(fullNameHeader, out var nameObj))
                    fullName = nameObj?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(fullName)) fullName = username.Split('@')[0];

                string role = "Student";
                if (!string.IsNullOrEmpty(roleHeader) && row.TryGetValue(roleHeader, out var roleObj))
                {
                    var roleStr = roleObj?.ToString()?.Trim().ToLower() ?? "";
                    if (roleStr.Contains("lecture") || roleStr.Contains("giảng") || roleStr.Contains("gv"))
                        role = "Lecturer";
                }

                string tier = "Free";
                if (!string.IsNullOrEmpty(tierHeader) && row.TryGetValue(tierHeader, out var tierObj))
                {
                    var tierStr = tierObj?.ToString()?.Trim().ToLower() ?? "";
                    if (tierStr.Contains("premium")) tier = "Premium";
                }

                string password = defaultPassword;
                if (!string.IsNullOrEmpty(passwordHeader) && row.TryGetValue(passwordHeader, out var passObj))
                {
                    var passStr = passObj?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(passStr)) password = passStr;
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    PasswordHash = _passwordHasher.Hash(password),
                    Role = role,
                    SubscriptionTier = tier,
                    FullName = fullName
                };

                await _userRepository.AddAsync(user);
                newUsers.Add((username, fullName, password, role));
                success++;
            }

            if (success > 0)
            {
                await _userRepository.SaveChangesAsync();

                foreach (var u in newUsers)
                {
                    // Chỉ gửi mail nếu username có dạng email
                    if (!u.Email.Contains("@")) continue;
                    try
                    {
                        await _emailService.SendUserAccountEmailAsync(u.Email, u.FullName, u.Email, u.Password, u.Role);
                    }
                    catch { /* log nhưng không dừng */ }
                }
            }

            return (success, skipped);
        }

        private static bool IsMatch(string header, params string[] keywords)
        {
            var clean = header.Trim().ToLower();
            return keywords.Any(k => clean.Contains(k));
        }
    }
}
