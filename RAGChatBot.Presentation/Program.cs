using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RAGChatBot.Application.Common.Interfaces;
using RAGChatBot.Application.Services;
using RAGChatBot.Infrastructure.Persistence;
using RAGChatBot.Infrastructure.Persistence.Repositories;
using RAGChatBot.Infrastructure.Security;
using RAGChatBot.Infrastructure.Storage;
using RAGChatBot.Infrastructure.Email;
using RAGChatBot.Domain.Models;

using RAGChatBot.Presentation.Services;
using RAGChatBot.Presentation.Middlewares;
using Serilog;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/ragchatbot-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// 1. Cấu hình EF Core với PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=rag_chatbot_db;Username=postgres;Password=your_password";
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
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "https://dssylnlnvftebqsodsnk.supabase.co";
var supabaseKey = builder.Configuration["Supabase:AnonKey"] ?? "your-anon-key";
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
            
            var testUsers = new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "lecturer_free",
                    PasswordHash = hasher.Hash("password123"),
                    Role = "Lecturer",
                    SubscriptionTier = "Free"
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "lecturer_premium",
                    PasswordHash = hasher.Hash("password123"),
                    Role = "Lecturer",
                    SubscriptionTier = "Premium"
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    PasswordHash = hasher.Hash("password123"),
                    Role = "Admin",
                    SubscriptionTier = "Premium"
                }
            };

            context.Users.AddRange(testUsers);
            context.SaveChanges();
            Console.WriteLine("[Database Seed] Ä ã tạo thành công các tài khoản thử nghiệm: lecturer_free, lecturer_premium, admin (mật khẩu chung: password123)");
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

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();


app.MapStaticAssets();

// Kích hoạt Middleware xác thực và phân quyền
// Kích hoạt Middleware xác thực và phân quyá» n
app.UseAuthentication();
app.UseAuthorization();

// Ä ăng ký các controller định tuyến (AccountController)
app.MapControllers();

// Ä ăng ký các Razor Pages
app.MapRazorPages();

app.MapHub<RAGChatBot.Presentation.Hubs.DocumentHub>("/documentHub");

app.Run();
