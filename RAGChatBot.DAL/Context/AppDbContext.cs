using Microsoft.EntityFrameworkCore;
using RAGChatBot.Domain.Constants;
using RAGChatBot.Domain.Entities;

namespace RAGChatBot.DAL.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
        public DbSet<WhitelistEmail> WhitelistEmails => Set<WhitelistEmail>();
        public DbSet<PerformanceBenchmark> PerformanceBenchmarks => Set<PerformanceBenchmark>();
        public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
        public DbSet<QuestionBank> QuestionBanks => Set<QuestionBank>();
        public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
        public DbSet<ChatThread> ChatThreads => Set<ChatThread>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<QuizAttemptAnswer> QuizAttemptAnswers => Set<QuizAttemptAnswer>();
        public DbSet<ChatTrackerLog> ChatTrackerLogs => Set<ChatTrackerLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Kích hoạt extension pgvector trong PostgreSQL
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<WhitelistEmail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.RoleId);
                entity.HasOne(e => e.Role)
                      .WithMany(role => role.Users)
                      .HasForeignKey(e => e.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name)
                      .HasMaxLength(50)
                      .IsRequired();
            });

            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrderId).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Type);
                entity.Property(e => e.OrderId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.TransactionNo).HasMaxLength(100);
                entity.Property(e => e.Type)
                      .HasMaxLength(50)
                      .HasDefaultValue(PaymentTransactionTypes.PremiumSubscription)
                      .IsRequired();
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
            });

            modelBuilder.Entity<KnowledgeDocument>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CourseCode);
            });

            modelBuilder.Entity<DocumentChunk>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DocumentId);
                entity.Property(e => e.Embedding)
                      .HasColumnType($"vector({AiConstants.VectorDimensions})"); // Chuẩn hóa số chiều từ hằng số
                
                entity.HasOne(e => e.Document)
                      .WithMany(d => d.Chunks)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PerformanceBenchmark>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.OperationType);
    entity.HasIndex(e => e.MeasuredAt);
});

modelBuilder.Entity<ChatSession>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.UserId);
    entity.HasIndex(e => e.CreatedAt);
    entity.HasIndex(e => new { e.UserId, e.UsageDate }).IsUnique();
});

modelBuilder.Entity<Quiz>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.CourseCode);
    entity.Property(e => e.PasswordHash).HasMaxLength(200);
});

modelBuilder.Entity<QuizAttempt>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.UserId, e.QuizId, e.AttemptNumber }).IsUnique();
    entity.HasIndex(e => new { e.QuizId, e.UserId, e.Status });
    entity.HasIndex(e => new { e.UserId, e.QuizId })
          .IsUnique()
          .HasFilter("\"Status\" = 0 AND \"QuizId\" IS NOT NULL");
    entity.Property(e => e.Version).IsRowVersion();
});

modelBuilder.Entity<QuizAttemptAnswer>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.AttemptId, e.DisplayOrder }).IsUnique();
    entity.HasOne(e => e.Attempt)
          .WithMany(attempt => attempt.Answers)
          .HasForeignKey(e => e.AttemptId)
          .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<ChatTrackerLog>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.UserId);
    entity.HasIndex(e => e.CreatedAt);
    entity.Property(e => e.Question).IsRequired();
    entity.Property(e => e.Answer).IsRequired();
});

        }
    }
}
