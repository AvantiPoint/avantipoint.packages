using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNpmUnpublishTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ArtifactDeleted",
                table: "NpmVersions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "NpmVersions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_NpmVersions_FeedId_IsDeleted_ArtifactDeleted",
                table: "NpmVersions",
                columns: new[] { "FeedId", "IsDeleted", "ArtifactDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NpmVersions_FeedId_IsDeleted_ArtifactDeleted",
                table: "NpmVersions");

            migrationBuilder.DropColumn(
                name: "ArtifactDeleted",
                table: "NpmVersions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "NpmVersions");
        }
    }
}
