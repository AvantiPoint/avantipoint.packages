using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AvantiPoint.Packages.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddOciRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OciBlobs",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OciSegment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Digest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciBlobs", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "OciManifests",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OciSegment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Digest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PlatformOs = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PlatformArch = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ArtifactKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Unknown"),
                    Origin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Published"),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciManifests", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "OciRepositories",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OciSegment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciRepositories", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "OciUploads",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UploadId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OciSegment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RepositoryName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    BytesReceived = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciUploads", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "OciTags",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OciSegment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RepositoryKey = table.Column<int>(type: "integer", nullable: false),
                    Tag = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ManifestDigest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Origin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Published")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciTags", x => x.Key);
                    table.ForeignKey(
                        name: "FK_OciTags_OciRepositories_RepositoryKey",
                        column: x => x.RepositoryKey,
                        principalTable: "OciRepositories",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OciBlobs_FeedId_OciSegment_Digest",
                table: "OciBlobs",
                columns: new[] { "FeedId", "OciSegment", "Digest" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OciManifests_FeedId_OciSegment_Digest",
                table: "OciManifests",
                columns: new[] { "FeedId", "OciSegment", "Digest" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OciRepositories_FeedId_OciSegment_Name",
                table: "OciRepositories",
                columns: new[] { "FeedId", "OciSegment", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OciTags_FeedId_OciSegment_RepositoryKey_Tag",
                table: "OciTags",
                columns: new[] { "FeedId", "OciSegment", "RepositoryKey", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OciTags_RepositoryKey",
                table: "OciTags",
                column: "RepositoryKey");

            migrationBuilder.CreateIndex(
                name: "IX_OciUploads_UploadId",
                table: "OciUploads",
                column: "UploadId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OciBlobs");

            migrationBuilder.DropTable(
                name: "OciManifests");

            migrationBuilder.DropTable(
                name: "OciTags");

            migrationBuilder.DropTable(
                name: "OciUploads");

            migrationBuilder.DropTable(
                name: "OciRepositories");
        }
    }
}
