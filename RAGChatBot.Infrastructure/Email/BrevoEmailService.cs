using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RAGChatBot.Application.Common.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace RAGChatBot.Infrastructure.Email
{
    public class BrevoEmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BrevoEmailService> _logger;
        private readonly string _apiKey;
        private readonly string _senderEmail;
        private readonly string _senderName;
        private readonly string _loginUrl;

        public BrevoEmailService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<BrevoEmailService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            var section = configuration.GetSection("EmailSettings");
            _apiKey = section["ApiKey"] ?? string.Empty;
            _senderEmail = section["SenderEmail"] ?? "no-reply@ragchatbot.com";
            _senderName = section["SenderName"] ?? "RAG ChatBot Admin";
            _loginUrl = section["LoginUrl"] ?? "http://localhost:5032/Account/Login";

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Brevo API Key chưa được cấu hình trong appsettings.json tại mục EmailSettings:ApiKey!");
            }
        }

        public async Task SendWelcomeEmailAsync(string toEmail, string? fullName)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Không gửi được email chào mừng do địa chỉ email trống!");
                return;
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Bỏ qua việc gửi email do chưa cấu hình API Key của Brevo.");
                return;
            }

            var displayName = !string.IsNullOrWhiteSpace(fullName) ? fullName : toEmail.Split('@')[0];

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var htmlContent = GetWelcomeEmailHtml(displayName, toEmail);

                var payload = new
                {
                    sender = new { name = _senderName, email = _senderEmail },
                    to = new[] { new { email = toEmail, name = displayName } },
                    subject = "Chào mừng bạn đến với RAG ChatBot",
                    htmlContent = htmlContent
                };

                request.Content = JsonContent.Create(payload);

                _logger.LogInformation("Đang gửi email chào mừng tới {Email} qua Brevo API...", toEmail);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Lỗi khi gửi email qua Brevo API. HTTP Status: {Status}, Response: {Response}", 
                        response.StatusCode, responseBody);
                    throw new Exception($"Lỗi gửi email: HTTP {response.StatusCode} - {responseBody}");
                }

                _logger.LogInformation("Đã gửi email chào mừng tới {Email} thành công qua Brevo API.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi gửi email chào mừng tới {Email}", toEmail);
                throw;
            }
        }

        public async Task SendUserAccountEmailAsync(string toEmail, string? fullName, string username, string password, string role)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return;
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Bỏ qua việc gửi email tài khoản do chưa cấu hình API Key của Brevo.");
                return;
            }

            var displayName = !string.IsNullOrWhiteSpace(fullName) ? fullName : toEmail.Split('@')[0];
            var roleDisplay = role == "Lecturer" ? "Giảng viên" : "Sinh viên";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", _apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var htmlContent = GetAccountCreatedEmailHtml(displayName, username, password, roleDisplay);

                var payload = new
                {
                    sender = new { name = _senderName, email = _senderEmail },
                    to = new[] { new { email = toEmail, name = displayName } },
                    subject = "Tài khoản RAG ChatBot của bạn đã được tạo",
                    htmlContent = htmlContent
                };

                request.Content = JsonContent.Create(payload);
                _logger.LogInformation("Đang gửi email thông tin tài khoản tới {Email}...", toEmail);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Lỗi Brevo khi gửi email tài khoản. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);
                    throw new Exception($"Lỗi gửi email: HTTP {response.StatusCode} - {responseBody}");
                }

                _logger.LogInformation("Đã gửi email thông tin tài khoản tới {Email} thành công.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi gửi email tài khoản tới {Email}", toEmail);
                throw;
            }
        }

        private string GetAccountCreatedEmailHtml(string fullName, string username, string password, string roleDisplay)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Tài khoản RAG ChatBot của bạn</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 30px auto; background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); }}
        .header {{ background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%); padding: 40px 20px; text-align: center; color: #fff; }}
        .header h1 {{ margin: 0; font-size: 26px; font-weight: 700; }}
        .header p {{ margin: 8px 0 0; opacity: 0.85; font-size: 14px; }}
        .content {{ padding: 36px 32px; line-height: 1.7; }}
        .credential-box {{ background: #f8f9ff; border: 1.5px solid #c7d2fe; border-radius: 10px; padding: 20px 24px; margin: 24px 0; }}
        .credential-row {{ display: flex; justify-content: space-between; align-items: center; padding: 8px 0; border-bottom: 1px dashed #e0e7ff; }}
        .credential-row:last-child {{ border-bottom: none; }}
        .credential-label {{ font-size: 13px; color: #6366f1; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; }}
        .credential-value {{ font-size: 15px; font-weight: 700; color: #1e1b4b; font-family: 'Courier New', monospace; background: #e0e7ff; padding: 4px 10px; border-radius: 6px; }}
        .btn {{ display: inline-block; background: linear-gradient(135deg, #6366f1, #4f46e5); color: #fff !important; text-decoration: none; padding: 13px 32px; border-radius: 8px; font-weight: 700; margin-top: 8px; }}
        .warning {{ background: #fffbeb; border-left: 4px solid #f59e0b; padding: 12px 16px; border-radius: 6px; font-size: 14px; color: #92400e; margin-top: 20px; }}
        .footer {{ background: #f9fafb; padding: 20px; text-align: center; font-size: 12px; color: #9ca3af; border-top: 1px solid #f3f4f6; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>RAG ChatBot</h1>
            <p>Thông tin tài khoản của bạn</p>
        </div>
        <div class=""content"">
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Tài khoản hệ thống <strong>RAG ChatBot</strong> của bạn đã được Quản trị viên tạo thành công với vai trò <strong>{roleDisplay}</strong>. Dưới đây là thông tin đăng nhập:</p>
            <div class=""credential-box"">
                <div class=""credential-row"">
                    <span class=""credential-label"">Tên đăng nhập</span>
                    <span class=""credential-value"">{username}</span>
                </div>
                <div class=""credential-row"">
                    <span class=""credential-label"">Mật khẩu</span>
                    <span class=""credential-value"">{password}</span>
                </div>
                <div class=""credential-row"">
                    <span class=""credential-label"">Vai trò</span>
                    <span class=""credential-value"">{roleDisplay}</span>
                </div>
            </div>
            <div style=""text-align: center; margin: 28px 0;"">
                <a href=""{_loginUrl}"" class=""btn"" style=""color: #ffffff !important;"">Đăng Nhập Ngay</a>
            </div>
            <div class=""warning"">
                <strong>⚠️ Bảo mật:</strong> Vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên để bảo vệ tài khoản của bạn.
            </div>
            <p style=""margin-top: 24px;"">Trân trọng,<br>Đội ngũ RAG ChatBot</p>
        </div>
        <div class=""footer"">Đây là email tự động. Vui lòng không phản hồi email này.</div>
    </div>
</body>
</html>";
        }

        private string GetWelcomeEmailHtml(string fullName, string email)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Chào mừng bạn đến với RAG ChatBot</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f7f6;
            color: #333333;
            margin: 0;
            padding: 0;
        }}
        .container {{
            max-width: 600px;
            margin: 30px auto;
            background-color: #ffffff;
            border-radius: 12px;
            overflow: hidden;
            box-shadow: 0 4px 15px rgba(0,0,0,0.05);
        }}
        .header {{
            background: linear-gradient(135deg, #0ea5e9 0%, #0284c7 100%);
            padding: 40px 20px;
            text-align: center;
            color: #ffffff;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
            font-weight: 600;
        }}
        .content {{
            padding: 40px 30px;
            line-height: 1.6;
        }}
        .content p {{
            margin: 0 0 20px 0;
            font-size: 16px;
        }}
        .btn {{
            display: inline-block;
            background: linear-gradient(135deg, #0ea5e9 0%, #0284c7 100%);
            color: #ffffff !important;
            text-decoration: none;
            padding: 12px 30px;
            border-radius: 8px;
            font-weight: 600;
            margin-top: 10px;
            box-shadow: 0 4px 10px rgba(14, 165, 233, 0.3);
        }}
        .footer {{
            background-color: #f9fafb;
            padding: 20px;
            text-align: center;
            font-size: 12px;
            color: #666666;
            border-top: 1px solid #eeeeee;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>RAG ChatBot</h1>
        </div>
        <div class=""content"">
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Tài khoản email của bạn (<strong>{email}</strong>) vừa được ban quản trị thêm vào danh sách trắng (Whitelist) của hệ thống <strong>RAG ChatBot - Hệ thống Quản trị & Trợ lý Tri thức AI</strong>.</p>
            <p>Bây giờ bạn đã có quyền truy cập và sử dụng các tính năng của hệ thống. Vui lòng bấm vào nút dưới đây để đăng nhập bằng tài khoản Google của bạn:</p>
            <div style=""text-align: center; margin: 30px 0;"">
                <a href=""{_loginUrl}"" class=""btn"" style=""color: #ffffff !important;"">Đăng Nhập Ngay</a>
            </div>
            <p>Nếu bạn gặp bất kỳ vấn đề gì trong quá trình đăng nhập hoặc sử dụng hệ thống, vui lòng liên hệ với Quản trị viên để được hỗ trợ.</p>
            <p>Trân trọng,<br>Đội ngũ RAG ChatBot</p>
        </div>
        <div class=""footer"">
            Đây là email tự động từ hệ thống RAG ChatBot. Vui lòng không phản hồi email này.
        </div>
    </div>
</body>
</html>";
        }
    }
}
