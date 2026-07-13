using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RAGChatBot.DAL.Context;

#nullable disable

namespace RAGChatBot.DAL.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713090000_AddDocumentChunkVectorIndex")]
    public partial class AddDocumentChunkVectorIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_DocumentChunks_Embedding_Hnsw\" " +
                "ON \"DocumentChunks\" USING hnsw (\"Embedding\" vector_cosine_ops);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS \"IX_DocumentChunks_Embedding_Hnsw\";");
        }
    }
}
