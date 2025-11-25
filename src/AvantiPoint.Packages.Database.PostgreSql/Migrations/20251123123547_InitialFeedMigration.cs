using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AvantiPoint.Packages.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialFeedMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FeedUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CachingStrategy = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Password = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ApiKey = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MirrorSignaturePolicy = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepositorySigningCertificates",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstUsed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ContentUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PublicCertificateBytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySigningCertificates", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "VulnerabilityRecords",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdvisoryUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VulnerabilityRecords", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "vw_PackageWithJsonData",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Authors = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Downloads = table.Column<long>(type: "bigint", nullable: false),
                    HasReadme = table.Column<bool>(type: "boolean", nullable: false),
                    HasEmbeddedIcon = table.Column<bool>(type: "boolean", nullable: false),
                    HasEmbeddedLicense = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrerelease = table.Column<bool>(type: "boolean", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Listed = table.Column<bool>(type: "boolean", nullable: false),
                    LicenseExpression = table.Column<string>(type: "text", nullable: true),
                    IsSigned = table.Column<bool>(type: "boolean", nullable: false),
                    IsTool = table.Column<bool>(type: "boolean", nullable: false),
                    IsDevelopmentDependency = table.Column<bool>(type: "boolean", nullable: false),
                    MinClientVersion = table.Column<string>(type: "text", nullable: true),
                    Published = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequireLicenseAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    SemVerLevel = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LicenseUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProjectUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RepositoryUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RepositoryType = table.Column<string>(type: "text", nullable: true),
                    RepositoryCommit = table.Column<string>(type: "text", nullable: true),
                    RepositoryCommitDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Tags = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                    DeprecationReasons = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DeprecationMessage = table.Column<string>(type: "text", nullable: true),
                    DeprecatedAlternatePackageId = table.Column<string>(type: "text", nullable: true),
                    DeprecatedAlternatePackageVersionRange = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
                    DependenciesJson = table.Column<string>(type: "text", nullable: true),
                    PackageTypesJson = table.Column<string>(type: "text", nullable: true),
                    TargetFrameworksJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vw_PackageWithJsonData", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Authors = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Downloads = table.Column<long>(type: "bigint", nullable: false),
                    HasReadme = table.Column<bool>(type: "boolean", nullable: false),
                    HasEmbeddedIcon = table.Column<bool>(type: "boolean", nullable: false),
                    HasEmbeddedLicense = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrerelease = table.Column<bool>(type: "boolean", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Listed = table.Column<bool>(type: "boolean", nullable: false),
                    LicenseExpression = table.Column<string>(type: "text", nullable: true),
                    IsSigned = table.Column<bool>(type: "boolean", nullable: false),
                    IsTool = table.Column<bool>(type: "boolean", nullable: false),
                    IsDevelopmentDependency = table.Column<bool>(type: "boolean", nullable: false),
                    MinClientVersion = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    Published = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequireLicenseAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    SemVerLevel = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LicenseUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProjectUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RepositoryUrl = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RepositoryType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepositoryCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RepositoryCommitDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Tags = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Origin = table.Column<string>(type: "text", nullable: false, defaultValue: "Published"),
                    PackageSourceId = table.Column<int>(type: "integer", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                    DeprecationReasons = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DeprecationMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DeprecatedAlternatePackageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DeprecatedAlternatePackageVersionRange = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Key);
                    table.ForeignKey(
                        name: "FK_Packages_PackageSources_PackageSourceId",
                        column: x => x.PackageSourceId,
                        principalTable: "PackageSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PackageVulnerabilities",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PackageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VersionRange = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VulnerabilityKey = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "PackageDependencies",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VersionRange = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TargetFramework = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PackageKey = table.Column<int>(type: "integer", nullable: false)
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
                name: "PackageDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageKey = table.Column<int>(type: "integer", nullable: false),
                    RemoteIp = table.Column<IPAddress>(type: "inet", nullable: true),
                    UserAgentString = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NuGetClient = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NuGetClientVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientPlatform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientPlatformVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    User = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "PackageTypes",
                columns: table => new
                {
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PackageKey = table.Column<int>(type: "integer", nullable: false)
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
                    Key = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Moniker = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PackageKey = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_PackageDownloads_PackageKey",
                table: "PackageDownloads",
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
                name: "IX_PackageTypes_Name",
                table: "PackageTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PackageTypes_PackageKey",
                table: "PackageTypes",
                column: "PackageKey");

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
                name: "IX_TargetFrameworks_Moniker",
                table: "TargetFrameworks",
                column: "Moniker");

            migrationBuilder.CreateIndex(
                name: "IX_TargetFrameworks_PackageKey",
                table: "TargetFrameworks",
                column: "PackageKey");

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilityRecords_AdvisoryUrl",
                table: "VulnerabilityRecords",
                column: "AdvisoryUrl");

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilityRecords_UpdatedUtc",
                table: "VulnerabilityRecords",
                column: "UpdatedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDependencies");

            migrationBuilder.DropTable(
                name: "PackageDownloads");

            migrationBuilder.DropTable(
                name: "PackageTypes");

            migrationBuilder.DropTable(
                name: "PackageVulnerabilities");

            migrationBuilder.DropTable(
                name: "RepositorySigningCertificates");

            migrationBuilder.DropTable(
                name: "TargetFrameworks");

            migrationBuilder.DropTable(
                name: "vw_PackageWithJsonData");

            migrationBuilder.DropTable(
                name: "VulnerabilityRecords");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "PackageSources");
        }
    }
}
