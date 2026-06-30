using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace RAGChatBot.Presentation.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Uncaught exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Set up response
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var html = $@"
                <!DOCTYPE html>
                <html lang='vi'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>System Error - RAG ChatBot</title>
                    <style>
                        body {{ font-family: 'Inter', sans-serif; background-color: #f8f9fa; color: #333; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; }}
                        .error-container {{ background: white; padding: 40px; border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); max-width: 600px; text-align: center; border-top: 4px solid #dc3545; }}
                        h1 {{ font-size: 24px; margin-bottom: 16px; color: #dc3545; }}
                        p {{ font-size: 15px; color: #666; margin-bottom: 24px; line-height: 1.6; }}
                        .btn {{ display: inline-block; padding: 10px 20px; background-color: #000; color: #fff; text-decoration: none; border-radius: 4px; font-weight: 500; transition: background 0.3s; }}
                        .btn:hover {{ background-color: #333; }}
                    </style>
                </head>
                <body>
                    <div class='error-container'>
                        <h1>Đã Xảy Ra Lỗi Hệ Thống</h1>
                        <p>Chúng tôi đã ghi nhận sự cố này và hệ thống đang xử lý. Bạn có thể thử lại sau hoặc quay về trang chủ.</p>
                        <a href='/' class='btn'>Về Trang Chủ</a>
                    </div>
                </body>
                </html>";

            return context.Response.WriteAsync(html);
        }
    }
}
