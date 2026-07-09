using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RAGChatBot.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class LogsModel : PageModel
    {
        private readonly IWebHostEnvironment _env;

        public LogsModel(IWebHostEnvironment env)
        {
            _env = env;
        }

        public List<string> LogFiles { get; set; } = new();
        public string SelectedFile { get; set; } = string.Empty;
        public List<LogLineDto> LogLines { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string LogFile { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string Level { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public int MaxLines { get; set; } = 500;

        public void OnGet()
        {
            var logsDir = Path.Combine(_env.ContentRootPath, "logs");
            if (!Directory.Exists(logsDir))
            {
                return;
            }

            // Lấy tất cả tệp log dạng ragchatbot-*.txt
            LogFiles = Directory.GetFiles(logsDir, "ragchatbot-*.txt")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .OrderByDescending(f => f)
                .ToList()!;

            if (LogFiles.Count == 0)
            {
                return;
            }

            // Chọn tệp log
            SelectedFile = string.IsNullOrEmpty(LogFile) ? LogFiles[0] : LogFile;
            if (!LogFiles.Contains(SelectedFile))
            {
                SelectedFile = LogFiles[0];
            }

            var filePath = Path.Combine(logsDir, SelectedFile);
            if (!System.IO.File.Exists(filePath))
            {
                return;
            }

            // Đọc dòng log bằng FileShare.ReadWrite để tránh xung đột khóa với Serilog đang chạy
            var rawLines = new List<string>();
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        rawLines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                LogLines.Add(new LogLineDto 
                { 
                    Level = "ERR", 
                    Message = $"Không thể đọc tệp log: {ex.Message}" 
                });
                return;
            }

            // Phân tích cú pháp các dòng log
            var parsedLines = new List<LogLineDto>();
            foreach (var line in rawLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var dto = ParseLogLine(line);
                
                // Lọc theo Cấp độ log
                if (Level != "All")
                {
                    if (Level.Equals("Error", StringComparison.OrdinalIgnoreCase) && dto.Level != "ERR") continue;
                    if (Level.Equals("Warning", StringComparison.OrdinalIgnoreCase) && dto.Level != "WRN") continue;
                    if (Level.Equals("Info", StringComparison.OrdinalIgnoreCase) && dto.Level != "INF") continue;
                }

                // Lọc theo Từ khóa tìm kiếm
                if (!string.IsNullOrEmpty(SearchTerm))
                {
                    if (!dto.RawContent.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) continue;
                }

                parsedLines.Add(dto);
            }

            // Lấy MaxLines dòng log mới nhất để hiển thị (đảo ngược để dòng mới nhất lên đầu hoặc xếp theo chiều thời gian)
            // Thông thường xem logs là hiển thị mới nhất ở trên cùng khi có phân trang, hoặc xếp xuôi. Ở đây hiển thị đảo ngược để dòng mới nhất lên trước.
            LogLines = parsedLines.AsEnumerable().Reverse().Take(MaxLines).ToList();
        }

        public IActionResult OnPostClearLog(string logFile)
        {
            if (string.IsNullOrEmpty(logFile)) return RedirectToPage();

            var logsDir = Path.Combine(_env.ContentRootPath, "logs");
            var filePath = Path.Combine(logsDir, logFile);
            
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        // File truncated successfully
                    }
                }
                catch (Exception)
                {
                    // Ignore or log error
                }
            }

            return RedirectToPage(new { LogFile = logFile });
        }

        private LogLineDto ParseLogLine(string line)
        {
            var dto = new LogLineDto { RawContent = line };

            // Phân tích định dạng log Serilog điển hình: 2026-07-09 14:09:41.551 +07:00 [INF] ...
            if (line.Length > 25 && (line[4] == '-' && line[7] == '-'))
            {
                var levelStart = line.IndexOf('[');
                var levelEnd = line.IndexOf(']');
                if (levelStart > 0 && levelEnd > levelStart)
                {
                    dto.Timestamp = line.Substring(0, levelStart).Trim();
                    dto.Level = line.Substring(levelStart + 1, levelEnd - levelStart - 1).Trim();
                    dto.Message = line.Substring(levelEnd + 1).Trim();
                }
                else
                {
                    dto.Message = line;
                }
            }
            else
            {
                dto.Message = line;
                dto.Level = "None";
            }

            return dto;
        }
    }

    public class LogLineDto
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty; // INF, WRN, ERR, None
        public string Message { get; set; } = string.Empty;
        public string RawContent { get; set; } = string.Empty;
    }
}
