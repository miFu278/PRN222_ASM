using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingSubscriptionExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionExpiresAt",
                table: "Users");
        }
    }
}
