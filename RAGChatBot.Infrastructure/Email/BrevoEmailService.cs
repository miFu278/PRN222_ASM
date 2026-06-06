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
