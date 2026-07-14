using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RAGChatBot.Domain.Interfaces;
using RAGChatBot.BLL.Services;
using RAGChatBot.DAL.Context;
using RAGChatBot.DAL.Repositories;
using RAGChatBot.DAL.Services;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;
using System.Security.Claims;
using System.Threading.RateLimiting;

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
    ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection in configuration.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, x => x.UseVector()));

// 2. Cấu hình Cookie Authentication & Google OAuth
const string externalCookieScheme = "ExternalCookie";
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddCookie(externalCookieScheme, options =>
    {
        options.Cookie.Name = "RAGChatBot.External";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddGoogle(options =>
    {
        options.SignInScheme = externalCookieScheme;
        var googleAuthNSection = builder.Configuration.GetSection("Authentication:Google");
        options.ClientId = googleAuthNSection["ClientId"]
            ?? throw new InvalidOperationException("Missing Authentication:Google:ClientId configuration.");
        options.ClientSecret = googleAuthNSection["ClientSecret"]
            ?? throw new InvalidOperationException("Missing Authentication:Google:ClientSecret configuration.");
    });

builder.Services.AddCascadingAuthenticationState();

// 3. Đăng ký Dependency Injection cho các tầng
// Tầng Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IWhitelistService, WhitelistService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IChatService, ChatService>();

// Tầng Infrastructure Services
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

// Đăng ký Supabase Client
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Missing Supabase:Url in configuration.");
var supabaseKey = builder.Configuration["Supabase:ServiceKey"]
    ?? builder.Configuration["Supabase:AnonKey"]
    ?? throw new InvalidOperationException("Missing Supabase:ServiceKey or Supabase:AnonKey in configuration.");
builder.Services.AddSingleton(provider => new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
{
    AutoConnectRealtime = false
}));

// Đăng ký dịch vụ lưu trữ file đám mây
builder.Services.AddScoped<IFileStorageService, SupabaseFileStorageService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IWhitelistRepository, WhitelistRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IQuizRepository, QuizRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IChatTrackerLogRepository, ChatTrackerLogRepository>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();

// Đăng ký dịch vụ RAG & AI (Tự động Chunking & Vector hóa)
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddScoped<ITextExtractor, TextExtractor>();
builder.Services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>();
builder.Services.AddHttpClient<IChatResponseService, OpenAiChatService>(); // Dịch vụ RAG Chatbot mới cho ASM02
builder.Services.AddHttpClient<IQuizGenerationService, OpenAiQuizGenerationService>();
builder.Services.AddHttpClient<IEmailService, BrevoEmailService>();

// Dịch vụ Credit & Thanh toán
builder.Services.AddScoped<ICreditService, CreditService>();

// Đăng ký PayOS Client & Service
var payosSection = builder.Configuration.GetSection("PayOS");
var payosClient = new PayOS.PayOSClient(new PayOS.PayOSOptions
{
    ClientId = payosSection["ClientId"] ?? throw new InvalidOperationException("Missing PayOS:ClientId"),
    ApiKey = payosSection["ApiKey"] ?? throw new InvalidOperationException("Missing PayOS:ApiKey"),
    ChecksumKey = payosSection["ChecksumKey"] ?? throw new InvalidOperationException("Missing PayOS:ChecksumKey")
});
builder.Services.AddSingleton(payosClient);
builder.Services.AddSingleton<IPayOSService>(sp => new PayOSService(
    sp.GetRequiredService<PayOS.PayOSClient>(),
    payosSection["ReturnUrl"] ?? "http://localhost:5178/Subscription/PaymentCallback",
    payosSection["CancelUrl"] ?? "http://localhost:5178/Subscription/PaymentCancelled"
));

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHostedService<DocumentProcessingWorker>();

// Dashboard & Benchmark services
builder.Services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// 4. Đăng ký Razor Pages
builder.Services.AddRazorPages();

// Đăng ký MVC Controllers (dành cho Authentication login/logout)
builder.Services.AddControllers();
 
// Đăng ký các dịch vụ HttpContextAccessor để hỗ trợ lấy thông tin User trong Blazor
builder.Services.AddHttpContextAccessor();
 
// Đăng ký Event Service cho Real-time UI updates
builder.Services.AddSingleton<RAGChatBot.Domain.Interfaces.IDocumentEventService, DocumentEventService>();
builder.Services.AddSingleton<DocumentEventService>(sp => (DocumentEventService)sp.GetRequiredService<RAGChatBot.Domain.Interfaces.IDocumentEventService>());
builder.Services.AddSingleton<RAGChatBot.Domain.Interfaces.ICourseEventService, CourseEventService>();
builder.Services.AddSingleton<RAGChatBot.Domain.Interfaces.IQuizEventService, QuizEventService>();

builder.Services.AddSignalR();

// 6. Cấu hình Rate Limiting chống spam AI Chat
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("StudentChatLimit", context =>
    {
        var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole == RoleNames.Student)
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
        var systemRoles = new[]
        {
            new Role { Id = SystemRoleIds.Admin, Name = RoleNames.Admin, Description = "System administrator" },
            new Role { Id = SystemRoleIds.Lecturer, Name = RoleNames.Lecturer, Description = "Lecturer and course manager" },
            new Role { Id = SystemRoleIds.Student, Name = RoleNames.Student, Description = "Student user" }
        };

        foreach (var role in systemRoles)
        {
            if (!context.Roles.Any(existingRole => existingRole.Id == role.Id || existingRole.Name == role.Name))
            {
                context.Roles.Add(role);
            }
        }
        context.SaveChanges();

        var seedDemoUsers = app.Environment.IsDevelopment() ||
            bool.TryParse(Environment.GetEnvironmentVariable("SEED_DEMO_USERS"), out var shouldSeed) && shouldSeed;
        if (!context.Users.Any() && seedDemoUsers)
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
                    RoleId = SystemRoleIds.Lecturer,
                    SubscriptionTier = "Free"
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "lecturer_premium",
                    PasswordHash = hasher.Hash(seedPassword),
                    RoleId = SystemRoleIds.Lecturer,
                    SubscriptionTier = "Premium",
                    SubscriptionExpiresAt = DateTime.UtcNow.AddYears(1)
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    PasswordHash = hasher.Hash(seedPassword),
                    RoleId = SystemRoleIds.Admin,
                    SubscriptionTier = "Premium",
                    SubscriptionExpiresAt = DateTime.UtcNow.AddYears(1)
                }
            };

            context.Users.AddRange(testUsers);
            context.SaveChanges();
            Console.WriteLine($"[Database Seed] Đã tạo tài khoản thử nghiệm: lecturer_free, lecturer_premium, admin (mật khẩu: {(Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") != null ? "[from SEED_ADMIN_PASSWORD]" : seedPassword)})");
        }
    }
    catch (Exception ex)
    {
        services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup")
            .LogCritical(ex, "Không thể migrate hoặc seed PostgreSQL; dừng ứng dụng để tránh chạy với schema sai.");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();


app.MapStaticAssets();

// Kích hoạt Middleware
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Đăng ký các controller định tuyến (AccountController)
app.MapControllers();
 
// Đăng ký các Razor Pages
app.MapRazorPages();

app.MapHub<RAGChatBot.Presentation.Hubs.DocumentHub>("/documentHub");
app.MapHub<RAGChatBot.Presentation.Hubs.CourseHub>("/courseHub");
app.MapHub<RAGChatBot.Presentation.Hubs.QuizHub>("/quizHub");

app.Run();

// Expose the top-level entry point to WebApplicationFactory in the E2E test project.
public partial class Program;
