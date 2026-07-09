using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.DAL.Migrations
{
    /// <inheritdoc />
    public partial class FixModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_CourseCode",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_UserId",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_QuestionBanks_CourseCode",
                table: "QuestionBanks");

            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_CourseCode",
                table: "ChatThreads");

            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SentAt",
                table: "ChatMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_CourseCode",
                table: "QuizAttempts",
                column: "CourseCode");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_UserId",
                table: "QuizAttempts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_CourseCode",
                table: "QuestionBanks",
                column: "CourseCode");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_CourseCode",
                table: "ChatThreads",
                column: "CourseCode");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SentAt",
                table: "ChatMessages",
                column: "SentAt");
        }
    }
}
