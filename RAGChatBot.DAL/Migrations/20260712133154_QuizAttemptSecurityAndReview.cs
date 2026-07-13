using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.DAL.Migrations
{
    /// <inheritdoc />
    public partial class QuizAttemptSecurityAndReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "Quizzes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Quizzes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewPolicy",
                table: "Quizzes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleOptions",
                table: "Quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleQuestions",
                table: "Quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "QuizAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReviewPolicy",
                table: "QuizAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "QuizAttempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "QuizAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "QuizAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Chapter",
                table: "QuestionBanks",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Preserve old attempts as submitted history and give attempts linked to a quiz
            // a stable sequence number before creating the unique index.
            migrationBuilder.Sql("""
                UPDATE "QuizAttempts"
                SET "Status" = 1,
                    "StartedAt" = "AttemptedAt",
                    "SubmittedAt" = "AttemptedAt";

                WITH numbered AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "UserId", "QuizId"
                               ORDER BY "AttemptedAt", "Id") AS attempt_number
                    FROM "QuizAttempts"
                    WHERE "QuizId" IS NOT NULL
                )
                UPDATE "QuizAttempts" AS attempts
                SET "AttemptNumber" = numbered.attempt_number
                FROM numbered
                WHERE attempts."Id" = numbered."Id";

                UPDATE "QuestionBanks" AS questions
                SET "Chapter" = documents."Chapter"
                FROM "KnowledgeDocuments" AS documents
                WHERE questions."DocumentId" = documents."Id";
                """);

            migrationBuilder.CreateTable(
                name: "QuizAttemptAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    OptionA = table.Column<string>(type: "text", nullable: false),
                    OptionB = table.Column<string>(type: "text", nullable: false),
                    OptionC = table.Column<string>(type: "text", nullable: false),
                    OptionD = table.Column<string>(type: "text", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "text", nullable: false),
                    SelectedAnswer = table.Column<string>(type: "text", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAttemptAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizAttemptAnswers_QuizAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "QuizAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_QuizId_UserId_Status",
                table: "QuizAttempts",
                columns: new[] { "QuizId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_UserId_QuizId_AttemptNumber",
                table: "QuizAttempts",
                columns: new[] { "UserId", "QuizId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttemptAnswers_AttemptId_DisplayOrder",
                table: "QuizAttemptAnswers",
                columns: new[] { "AttemptId", "DisplayOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizAttemptAnswers");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_QuizId_UserId_Status",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_UserId_QuizId_AttemptNumber",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "ReviewPolicy",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "ShuffleOptions",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "ShuffleQuestions",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "ReviewPolicy",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "Chapter",
                table: "QuestionBanks");
        }
    }
}
