using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.DAL.Migrations
{
    /// <inheritdoc />
    public partial class HardenPostMergeFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Quizzes",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "QuizAttempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "QuizAttempts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<DateOnly>(
                name: "UsageDate",
                table: "ChatSessions",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.Sql("""
                UPDATE "QuizAttempts" AS attempt
                SET "ExpiresAt" = attempt."StartedAt" + make_interval(mins => COALESCE(quiz."DurationMinutes", 30))
                FROM "Quizzes" AS quiz
                WHERE attempt."QuizId" = quiz."Id";

                UPDATE "QuizAttempts"
                SET "ExpiresAt" = "StartedAt" + interval '30 minutes'
                WHERE "ExpiresAt" < timestamp with time zone '2000-01-01 00:00:00+00';

                WITH duplicate_attempts AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "UserId", "QuizId"
                        ORDER BY "StartedAt" DESC, "Id") AS row_number
                    FROM "QuizAttempts"
                    WHERE "Status" = 0 AND "QuizId" IS NOT NULL
                )
                UPDATE "QuizAttempts" AS attempt
                SET "Status" = 1,
                    "SubmittedAt" = LEAST(NOW(), attempt."ExpiresAt"),
                    "AttemptedAt" = LEAST(NOW(), attempt."ExpiresAt")
                FROM duplicate_attempts
                WHERE attempt."Id" = duplicate_attempts."Id"
                  AND duplicate_attempts.row_number > 1;

                UPDATE "ChatSessions"
                SET "UsageDate" = ("CreatedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;

                WITH usage_totals AS (
                    SELECT DISTINCT ON ("UserId", "UsageDate")
                        "UserId",
                        "UsageDate",
                        "Id" AS keep_id,
                        SUM("MessageCount") OVER (
                            PARTITION BY "UserId", "UsageDate") AS total_count
                    FROM "ChatSessions"
                    ORDER BY "UserId", "UsageDate", "Id"
                )
                UPDATE "ChatSessions" AS session
                SET "MessageCount" = usage_totals.total_count
                FROM usage_totals
                WHERE session."Id" = usage_totals.keep_id;

                WITH duplicate_sessions AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "UserId", "UsageDate"
                        ORDER BY "Id") AS row_number
                    FROM "ChatSessions"
                )
                DELETE FROM "ChatSessions" AS session
                USING duplicate_sessions
                WHERE session."Id" = duplicate_sessions."Id"
                  AND duplicate_sessions.row_number > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_UserId_QuizId",
                table: "QuizAttempts",
                columns: new[] { "UserId", "QuizId" },
                unique: true,
                filter: "\"Status\" = 0 AND \"QuizId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_UserId_UsageDate",
                table: "ChatSessions",
                columns: new[] { "UserId", "UsageDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuizAttempts_UserId_QuizId",
                table: "QuizAttempts");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_UserId_UsageDate",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "QuizAttempts");

            migrationBuilder.DropColumn(
                name: "UsageDate",
                table: "ChatSessions");
        }
    }
}
