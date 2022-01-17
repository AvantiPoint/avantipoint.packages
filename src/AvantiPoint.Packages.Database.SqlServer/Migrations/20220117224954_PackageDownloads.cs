using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    public partial class PackageDownloads : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageKey = table.Column<int>(type: "int", nullable: false),
                    RemoteIp = table.Column<string>(type: "nvarchar(45)", nullable: true),
                    UserAgentString = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NuGetClient = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NuGetClientVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientPlatform = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientPlatformVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDownloads");
        }
    }
}
