using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMember1Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkSize",
                table: "KnowledgeDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChunkingStrategy",
                table: "KnowledgeDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Overlap",
                table: "KnowledgeDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChatThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseCode = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestionBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseCode = table.Column<string>(type: "text", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    OptionA = table.Column<string>(type: "text", nullable: false),
                    OptionB = table.Column<string>(type: "text", nullable: false),
                    OptionC = table.Column<string>(type: "text", nullable: false),
                    OptionD = table.Column<string>(type: "text", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBanks_KnowledgeDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "KnowledgeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseCode = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    Percentage = table.Column<double>(type: "double precision", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SentAt",
                table: "ChatMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ThreadId",
                table: "ChatMessages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_CourseCode",
                table: "ChatThreads",
                column: "CourseCode");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_CourseCode",
                table: "QuestionBanks",
                column: "CourseCode");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_DocumentId",
                table: "QuestionBanks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_CourseCode",
                table: "QuizAttempts",
                column: "CourseCode");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_UserId",
                table: "QuizAttempts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "QuestionBanks");

            migrationBuilder.DropTable(
                name: "QuizAttempts");

            migrationBuilder.DropTable(
                name: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "ChunkSize",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "ChunkingStrategy",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "Overlap",
                table: "KnowledgeDocuments");
        }
    }
}
