using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Application.Services;
using RAGChatBot.Infrastructure.Persistence;
using RAGChatBot.Infrastructure.Persistence.Repositories;
using RAGChatBot.Infrastructure.Security;
using RAGChatBot.Infrastructure.Storage;
using RAGChatBot.Infrastructure.Email;
using RAGChatBot.Domain.Models;
using System.Security.Claims;
using System.Threading.RateLimiting;

using RAGChatBot.Presentation.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình EF Core với PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection in configuration.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, x => x.UseVector()));

// 2. Cấu hình Cookie Authentication & Google OAuth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    })
    .AddGoogle(options =>
    {
        var googleAuthNSection = builder.Configuration.GetSection("Authentication:Google");
        options.ClientId = googleAuthNSection["ClientId"];
        options.ClientSecret = googleAuthNSection["ClientSecret"];
    });

builder.Services.AddCascadingAuthenticationState();

// 3. Đăng ký Dependency Injection cho các tầng
// Tầng Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IWhitelistService, WhitelistService>();

// Tầng Infrastructure Services
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

// Đăng ký Supabase Client
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Missing Supabase:Url in configuration.");
var supabaseKey = builder.Configuration["Supabase:AnonKey"]
    ?? throw new InvalidOperationException("Missing Supabase:AnonKey in configuration.");
builder.Services.AddSingleton(provider => new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
{
    AutoConnectRealtime = false
}));

// Đăng ký dịch vụ lưu trữ file đám mây
builder.Services.AddScoped<IFileStorageService, SupabaseFileStorageService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IWhitelistRepository, WhitelistRepository>();

// Ä ăng ký dịch vụ RAG & AI (Tự động Chunking & Vector hóa)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddScoped<ITextExtractor, TextExtractor>();
builder.Services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>();
builder.Services.AddHttpClient<IChatService, OpenAiChatService>(); // Dịch vụ RAG Chatbot mới cho ASM02
builder.Services.AddHttpClient<IEmailService, BrevoEmailService>();
builder.Services.AddHostedService<DocumentProcessingWorker>();

// 4. Ä ăng ký Razor Pages
builder.Services.AddRazorPages();

// Ä ăng ký MVC Controllers (dành cho Authentication login/logout)
builder.Services.AddControllers();

// Ä ăng ký các dịch vụ HttpContextAccessor để hỗ trợ lấy thông tin User trong Blazor
builder.Services.AddHttpContextAccessor();

// Ä ăng ký Event Service cho Real-time UI updates
builder.Services.AddSingleton<RAGChatBot.Application.Common.Interfaces.IDocumentEventService, DocumentEventService>();
builder.Services.AddSingleton<DocumentEventService>(sp => (DocumentEventService)sp.GetRequiredService<RAGChatBot.Application.Common.Interfaces.IDocumentEventService>());

builder.Services.AddSignalR();

// 6. Cấu hình Rate Limiting chống spam AI Chat
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("StudentChatLimit", context =>
    {
        var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole == "Student")
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? context.Connection.RemoteIpAddress?.ToString()
                         ?? "anonymous";
            return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }
        return RateLimitPartition.GetNoLimiter("unlimited");
    });
});

var app = builder.Build();

// 5. Tự động chạy Migration & Seed dữ liệu thử nghiệm khi khởi động
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        
        // Tự động migrate database khi ứng dụng chạy
        context.Database.Migrate();

        // Seed dữ liệu mẫu nếu bảng Users trống
        if (!context.Users.Any())
        {
            var hasher = services.GetRequiredService<IPasswordHasher>();
            var seedPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD")
                               ?? Guid.NewGuid().ToString();
            
            var testUsers = new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "lecturer_free",
                    PasswordHash = hasher.Hash(seedPassword),
                    Role = "Lecturer",
                    SubscriptionTier = "Free"
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "lecturer_premium",
                    PasswordHash = hasher.Hash(seedPassword),
                    Role = "Lecturer",
                    SubscriptionTier = "Premium"
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    PasswordHash = hasher.Hash(seedPassword),
                    Role = "Admin",
                    SubscriptionTier = "Premium"
                }
            };

            context.Users.AddRange(testUsers);
            context.SaveChanges();
            Console.WriteLine($"[Database Seed] Đã tạo tài khoản thử nghiệm: lecturer_free, lecturer_premium, admin (mật khẩu: {(Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") != null ? "[from SEED_ADMIN_PASSWORD]" : seedPassword)})");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database Error] Không thể kết nối hoặc khởi tạo dữ liệu PostgreSQL: {ex.Message}");
    }

    // Kiểm tra kết nối AI API
    try
    {
        var config = services.GetRequiredService<IConfiguration>();
        var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
        using var client = httpClientFactory.CreateClient();
        
        var baseUrl = config["AiSettings:BaseUrl"];
        var apiKey = config["AiSettings:ApiKey"];
        
        if (!string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            // Ä ối với Google Gemini dùng OpenAI interface, thử lấy danh sách model
            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/models");
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[API Check] KẾT Ná» I THÀNH CÔNG TỚI AI API.");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[API Error] LỖI KẾT Ná» I AI API ({response.StatusCode}): {errorContent}");
            }
        }
        else
        {
            Console.WriteLine("[API Check] Thiếu cấu hình AiSettings:BaseUrl hoặc AiSettings:ApiKey trong appsettings.json.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[API Error] Không thể kiểm tra kết nối AI API: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();


app.MapStaticAssets();

// Kích hoạt Middleware
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Ä ăng ký các controller định tuyến (AccountController)
app.MapControllers();

// Ä ăng ký các Razor Pages
app.MapRazorPages();

app.MapHub<RAGChatBot.Presentation.Hubs.DocumentHub>("/documentHub");

app.Run();
