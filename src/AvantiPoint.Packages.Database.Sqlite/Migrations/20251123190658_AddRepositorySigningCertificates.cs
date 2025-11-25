using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
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
                type: "TEXT",
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<int>(
                name: "PackageSourceId",
                table: "Packages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackageSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FeedUrl = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    CachingStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Password = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    MirrorSignaturePolicy = table.Column<string>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSyncAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSyncSuccessAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepositorySigningCertificates",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    HashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FirstUsed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ContentUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PublicCertificateBytes = table.Column<byte[]>(type: "BLOB", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySigningCertificates", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "VulnerabilityRecords",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdvisoryUrl = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VulnerabilityRecords", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "PackageVulnerabilities",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    VersionRange = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    VulnerabilityKey = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageVulnerabilities", x => x.Key);
                    table.ForeignKey(
                        name: "FK_PackageVulnerabilities_VulnerabilityRecords_VulnerabilityKey",
                        column: x => x.VulnerabilityKey,
                        principalTable: "VulnerabilityRecords",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_PackageVulnerabilities_PackageId",
                table: "PackageVulnerabilities",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageVulnerabilities_PackageId_VersionRange",
                table: "PackageVulnerabilities",
                columns: new[] { "PackageId", "VersionRange" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageVulnerabilities_VulnerabilityKey",
                table: "PackageVulnerabilities",
                column: "VulnerabilityKey");

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

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilityRecords_AdvisoryUrl",
                table: "VulnerabilityRecords",
                column: "AdvisoryUrl");

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilityRecords_UpdatedUtc",
                table: "VulnerabilityRecords",
                column: "UpdatedUtc");

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
                name: "PackageVulnerabilities");

            migrationBuilder.DropTable(
                name: "RepositorySigningCertificates");

            migrationBuilder.DropTable(
                name: "VulnerabilityRecords");

            migrationBuilder.DropIndex(
                name: "IX_Packages_PackageSourceId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "PackageSourceId",
                table: "Packages");

            // Views are handled by a subsequent migration and are not recreated here.
        }
    }
}
