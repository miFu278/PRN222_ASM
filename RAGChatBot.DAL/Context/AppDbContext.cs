using Microsoft.EntityFrameworkCore;
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
                entity.Property(e => e.OrderId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.TransactionNo).HasMaxLength(100);
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
                      .HasColumnType("vector(1536)"); // 1536 chiều (chuẩn OpenAI text-embedding-3-small)
                
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
});

modelBuilder.Entity<Quiz>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.CourseCode);
});

        }
    }
}
