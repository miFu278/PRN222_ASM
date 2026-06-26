using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAGChatBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUploaderName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UploaderName",
                table: "KnowledgeDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UploaderName",
                table: "KnowledgeDocuments");
        }
    }
}
