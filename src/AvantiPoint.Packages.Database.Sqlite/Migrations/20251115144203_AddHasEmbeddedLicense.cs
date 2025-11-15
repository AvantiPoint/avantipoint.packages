using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddHasEmbeddedLicense : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasEmbeddedLicense",
                table: "Packages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PackageDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PackageKey = table.Column<int>(type: "INTEGER", nullable: false),
                    RemoteIp = table.Column<string>(type: "TEXT", nullable: true),
                    UserAgentString = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NuGetClient = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    NuGetClientVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ClientPlatform = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ClientPlatformVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    User = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageDownloads_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageDownloads_PackageKey",
                table: "PackageDownloads",
                column: "PackageKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDownloads");

            migrationBuilder.DropColumn(
                name: "HasEmbeddedLicense",
                table: "Packages");
        }
    }
}
