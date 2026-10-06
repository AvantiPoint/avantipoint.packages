using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNativeArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NativeArtifacts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    FeedId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    PathHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Length = table.Column<long>(type: "INTEGER", nullable: false),
                    PackageName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NativeArtifacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NativeArtifacts_FeedId_Protocol_PackageName",
                table: "NativeArtifacts",
                columns: new[] { "FeedId", "Protocol", "PackageName" });

            migrationBuilder.CreateIndex(
                name: "IX_NativeArtifacts_FeedId_Protocol_PathHash",
                table: "NativeArtifacts",
                columns: new[] { "FeedId", "Protocol", "PathHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NativeArtifacts");
        }
    }
}
