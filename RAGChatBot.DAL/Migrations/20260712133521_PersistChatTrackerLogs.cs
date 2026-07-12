using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.DAL.Migrations
{
    /// <inheritdoc />
    public partial class PersistChatTrackerLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatTrackerLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    CourseCode = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTrackerLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTrackerLogs_CreatedAt",
                table: "ChatTrackerLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatTrackerLogs_UserId",
                table: "ChatTrackerLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatTrackerLogs");
        }
    }
}
