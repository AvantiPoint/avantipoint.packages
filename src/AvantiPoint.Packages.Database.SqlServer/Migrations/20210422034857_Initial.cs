using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Authors = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Downloads = table.Column<long>(type: "bigint", nullable: false),
                    HasReadme = table.Column<bool>(type: "bit", nullable: false),
                    HasEmbeddedIcon = table.Column<bool>(type: "bit", nullable: false),
                    IsPrerelease = table.Column<bool>(type: "bit", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Listed = table.Column<bool>(type: "bit", nullable: false),
                    LicenseExpression = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSigned = table.Column<bool>(type: "bit", nullable: false),
                    IsTool = table.Column<bool>(type: "bit", nullable: false),
                    IsDevelopmentDependency = table.Column<bool>(type: "bit", nullable: false),
                    MinClientVersion = table.Column<string>(type: "nvarchar(44)", maxLength: 44, nullable: true),
                    Published = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequireLicenseAcceptance = table.Column<bool>(type: "bit", nullable: false),
                    SemVerLevel = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IconUrl = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LicenseUrl = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ProjectUrl = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RepositoryUrl = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RepositoryType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OriginalVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "PackageDependencies",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VersionRange = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TargetFramework = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PackageKey = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDependencies", x => x.Key);
                    table.ForeignKey(
                        name: "FK_PackageDependencies_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageTypes",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PackageKey = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageTypes", x => x.Key);
                    table.ForeignKey(
                        name: "FK_PackageTypes_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TargetFrameworks",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Moniker = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PackageKey = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetFrameworks", x => x.Key);
                    table.ForeignKey(
                        name: "FK_TargetFrameworks_Packages_PackageKey",
                        column: x => x.PackageKey,
                        principalTable: "Packages",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_Id",
                table: "PackageDependencies",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PackageDependencies_PackageKey",
                table: "PackageDependencies",
                column: "PackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Id",
                table: "Packages",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Id_Version",
                table: "Packages",
                columns: new[] { "Id", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageTypes_Name",
                table: "PackageTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PackageTypes_PackageKey",
                table: "PackageTypes",
                column: "PackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_TargetFrameworks_Moniker",
                table: "TargetFrameworks",
                column: "Moniker");

            migrationBuilder.CreateIndex(
                name: "IX_TargetFrameworks_PackageKey",
                table: "TargetFrameworks",
                column: "PackageKey");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDependencies");

            migrationBuilder.DropTable(
                name: "PackageTypes");

            migrationBuilder.DropTable(
                name: "TargetFrameworks");

            migrationBuilder.DropTable(
                name: "Packages");
        }
    }
}
