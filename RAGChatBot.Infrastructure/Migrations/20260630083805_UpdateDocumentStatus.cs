using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDocumentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "KnowledgeDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Migrate data from IsProcessed to Status
            migrationBuilder.Sql("UPDATE \"KnowledgeDocuments\" SET \"Status\" = 2 WHERE \"IsProcessed\" = true;");
            migrationBuilder.Sql("UPDATE \"KnowledgeDocuments\" SET \"Status\" = 0 WHERE \"IsProcessed\" = false;");

            migrationBuilder.DropColumn(
                name: "IsProcessed",
                table: "KnowledgeDocuments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProcessed",
                table: "KnowledgeDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Rollback data
            migrationBuilder.Sql("UPDATE \"KnowledgeDocuments\" SET \"IsProcessed\" = true WHERE \"Status\" = 2;");
            migrationBuilder.Sql("UPDATE \"KnowledgeDocuments\" SET \"IsProcessed\" = false WHERE \"Status\" != 2;");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "KnowledgeDocuments");
        }
    }
}
