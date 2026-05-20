using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AvantiPoint.Packages.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiFeedPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packages_Id_Version",
                table: "Packages");

            migrationBuilder.AddColumn<string>(
                name: "FeedId",
                table: "Packages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.CreateTable(
                name: "NpmPackages",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpmPackages", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "NpmDistTags",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PackageKey = table.Column<int>(type: "integer", nullable: false),
                    Tag = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpmDistTags", x => x.Key);
                    table.ForeignKey(
                        name: "FK_NpmDistTags_NpmPackages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "NpmPackages",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpmVersions",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PackageKey = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TarballPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Shasum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Origin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Published"),
                    PackumentJson = table.Column<string>(type: "text", nullable: false),
                    Published = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpmVersions", x => x.Key);
                    table.ForeignKey(
                        name: "FK_NpmVersions_NpmPackages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "NpmPackages",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_FeedId_Id",
                table: "Packages",
                columns: new[] { "FeedId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_FeedId_Id_Version",
                table: "Packages",
                columns: new[] { "FeedId", "Id", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpmDistTags_FeedId_PackageKey_Tag",
                table: "NpmDistTags",
                columns: new[] { "FeedId", "PackageKey", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpmDistTags_PackageKey",
                table: "NpmDistTags",
                column: "PackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_NpmPackages_FeedId_Name",
                table: "NpmPackages",
                columns: new[] { "FeedId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpmVersions_FeedId_PackageKey_Version",
                table: "NpmVersions",
                columns: new[] { "FeedId", "PackageKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpmVersions_PackageKey",
                table: "NpmVersions",
                column: "PackageKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NpmDistTags");

            migrationBuilder.DropTable(
                name: "NpmVersions");

            migrationBuilder.DropTable(
                name: "NpmPackages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_FeedId_Id",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_FeedId_Id_Version",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "FeedId",
                table: "Packages");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Id_Version",
                table: "Packages",
                columns: new[] { "Id", "Version" },
                unique: true);
        }
    }
}
