using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.PostgreSql.Migrations
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
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Protocol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PathHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    PackageName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    PublishedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
