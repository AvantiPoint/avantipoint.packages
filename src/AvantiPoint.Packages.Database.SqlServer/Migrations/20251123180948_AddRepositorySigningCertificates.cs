using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositorySigningCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<int>(
                name: "PackageSourceId",
                table: "Packages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackageSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FeedUrl = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CachingStrategy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ApiKey = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MirrorSignaturePolicy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSyncAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSyncSuccessAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepositorySigningCertificates",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HashAlgorithm = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Issuer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirstUsed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ContentUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PublicCertificateBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySigningCertificates", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_PackageSourceId",
                table: "Packages",
                column: "PackageSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageSources_IsEnabled",
                table: "PackageSources",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_PackageSources_Name",
                table: "PackageSources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_Fingerprint_HashAlgorithm",
                table: "RepositorySigningCertificates",
                columns: new[] { "Fingerprint", "HashAlgorithm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_FirstUsed",
                table: "RepositorySigningCertificates",
                column: "FirstUsed");

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_IsActive_NotBefore_NotAfter",
                table: "RepositorySigningCertificates",
                columns: new[] { "IsActive", "NotBefore", "NotAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_LastUsed",
                table: "RepositorySigningCertificates",
                column: "LastUsed");

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_PackageSources_PackageSourceId",
                table: "Packages",
                column: "PackageSourceId",
                principalTable: "PackageSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packages_PackageSources_PackageSourceId",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "PackageSources");

            migrationBuilder.DropTable(
                name: "RepositorySigningCertificates");

            migrationBuilder.DropIndex(
                name: "IX_Packages_PackageSourceId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "PackageSourceId",
                table: "Packages");
        }
    }
}
