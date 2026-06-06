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

var builder = WebApplication.CreateBuilder(args);

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

// Đăng ký dịch vụ RAG & AI (Tự động Chunking & Vector hóa)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddScoped<ITextExtractor, TextExtractor>();
builder.Services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>();
builder.Services.AddHttpClient<IEmailService, BrevoEmailService>();
builder.Services.AddHostedService<DocumentProcessingWorker>();

// 4. Thêm cấu hình MVC Controllers & Views
builder.Services.AddControllersWithViews();

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
            Console.WriteLine("[Database Seed] Đã tạo thành công các tài khoản thử nghiệm: lecturer_free, lecturer_premium, admin (mật khẩu chung: password123)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Database Error] Không thể kết nối hoặc khởi tạo dữ liệu PostgreSQL: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Thêm Middleware xác thực và phân quyền
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
