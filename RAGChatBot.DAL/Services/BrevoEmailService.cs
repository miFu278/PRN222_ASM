using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Interfaces;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace RAGChatBot.DAL.Services
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
            var appBaseUrl = configuration["AppUrls:BaseUrl"] ?? "http://localhost:5178";
            _loginUrl = section["LoginUrl"] ?? $"{appBaseUrl}/Account/Login";

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
            var roleDisplay = role == RoleNames.Lecturer ? "Giảng viên" : "Sinh viên";

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
<html lang=""vi"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Tài khoản RAG ChatBot của bạn</title>
</head>
<body style=""margin:0; padding:0; background-color:#f5f0eb; font-family: Georgia, 'Times New Roman', serif;"">

    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f5f0eb;"">
        <tr>
            <td align=""center"" style=""padding: 40px 16px;"">

                <!-- Container -->
                <table width=""560"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:560px; width:100%; background-color:#faf7f2; border-radius:4px; overflow:hidden;"">

                    <!-- Header -->
                    <tr>
                        <td style=""padding: 48px 40px 40px; text-align:center; border-bottom: 1px solid #d9cfc4;"">
                            <div style=""display:inline-block; width:48px; height:2px; background:#8b7355; margin-bottom:20px;""></div>
                            <h1 style=""margin:0 0 6px; font-size:22px; font-weight:400; color:#3d3530; letter-spacing:3px; text-transform:uppercase;"">RAG ChatBot</h1>
                            <p style=""margin:0; font-size:12px; color:#9c8e80; letter-spacing:2px; text-transform:uppercase;"">Thông tin tài khoản</p>
                            <div style=""display:inline-block; width:48px; height:2px; background:#8b7355; margin-top:20px;""></div>
                        </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding: 40px 40px 32px;"">
                            <p style=""margin:0 0 10px; font-size:15px; color:#5c4f45; line-height:1.8;"">Xin chào <strong style=""color:#3d3530; font-weight:600;"">{fullName}</strong>,</p>
                            <p style=""margin:0 0 32px; font-size:14px; color:#7a6e65; line-height:1.9;"">Tài khoản hệ thống <strong style=""color:#5c4f45;"">RAG ChatBot</strong> của bạn đã được Quản trị viên tạo thành công với vai trò <strong style=""color:#5c4f45;"">{roleDisplay}</strong>. Dưới đây là thông tin đăng nhập:</p>

                            <!-- Credential Table -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f0ebe4; border-radius:3px; border: 1px solid #d9cfc4;"">
                                <tr>
                                    <td style=""padding: 14px 20px; border-bottom: 1px solid #d9cfc4;"">
                                        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                            <tr>
                                                <td width=""40%"" style=""font-size:11px; color:#8b7355; letter-spacing:1.5px; text-transform:uppercase; font-family: Georgia, serif; vertical-align:middle;"">TÊN ĐĂNG NHẬP</td>
                                                <td width=""60%"" style=""text-align:right; vertical-align:middle;"">
                                                    <span style=""display:inline-block; font-size:13px; font-weight:600; color:#3d3530; font-family:'Courier New', Courier, monospace; background:#e5ddd3; padding:4px 12px; border-radius:2px; word-break:break-all;"">{username}</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding: 14px 20px; border-bottom: 1px solid #d9cfc4;"">
                                        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                            <tr>
                                                <td width=""40%"" style=""font-size:11px; color:#8b7355; letter-spacing:1.5px; text-transform:uppercase; font-family: Georgia, serif; vertical-align:middle;"">MẬT KHẨU</td>
                                                <td width=""60%"" style=""text-align:right; vertical-align:middle;"">
                                                    <span style=""display:inline-block; font-size:13px; font-weight:600; color:#3d3530; font-family:'Courier New', Courier, monospace; background:#e5ddd3; padding:4px 12px; border-radius:2px;"">{password}</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding: 14px 20px;"">
                                        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                            <tr>
                                                <td width=""40%"" style=""font-size:11px; color:#8b7355; letter-spacing:1.5px; text-transform:uppercase; font-family: Georgia, serif; vertical-align:middle;"">VAI TRÒ</td>
                                                <td width=""60%"" style=""text-align:right; vertical-align:middle;"">
                                                    <span style=""display:inline-block; font-size:13px; font-weight:600; color:#3d3530; font-family:'Courier New', Courier, monospace; background:#e5ddd3; padding:4px 12px; border-radius:2px;"">{roleDisplay}</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>

                            <!-- CTA Button -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-top:32px;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""{_loginUrl}"" style=""display:inline-block; background-color:#5c4f45; color:#faf7f2 !important; text-decoration:none; font-size:12px; letter-spacing:2px; text-transform:uppercase; padding:14px 36px; border-radius:2px;"">Đăng Nhập Ngay</a>
                                    </td>
                                </tr>
                            </table>

                            <!-- Warning -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-top:28px; border-left: 3px solid #a0845c;"">
                                <tr>
                                    <td style=""padding: 12px 16px; background-color:#f5ede2;"">
                                        <p style=""margin:0; font-size:13px; color:#7a5c3a; line-height:1.7;""><strong>⚠ Lưu ý bảo mật:</strong> Vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên để bảo vệ tài khoản của bạn.</p>
                                    </td>
                                </tr>
                            </table>

                            <p style=""margin-top:36px; margin-bottom:0; font-size:14px; color:#7a6e65; line-height:1.9;"">Trân trọng,<br><span style=""color:#5c4f45; font-weight:600;"">Đội ngũ RAG ChatBot</span></p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""padding: 20px 40px; text-align:center; border-top: 1px solid #d9cfc4;"">
                            <p style=""margin:0; font-size:11px; color:#b0a395; letter-spacing:0.5px;"">Đây là email tự động. Vui lòng không phản hồi email này.</p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>

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
