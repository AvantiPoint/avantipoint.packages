using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace AvantiPoint.Packages.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class InitialFeedMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PackageSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    FeedUrl = table.Column<string>(type: "longtext", maxLength: 4000, nullable: false),
                    Type = table.Column<string>(type: "longtext", nullable: false),
                    CachingStrategy = table.Column<string>(type: "longtext", nullable: false),
                    Username = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    Password = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    ApiKey = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MirrorSignaturePolicy = table.Column<string>(type: "longtext", nullable: false),
                    Metadata = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LastSyncAttemptAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LastSyncSuccessAt = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LastError = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageSources", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RepositorySigningCertificates",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Fingerprint = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    HashAlgorithm = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Issuer = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FirstUsed = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    ContentUrl = table.Column<string>(type: "longtext", maxLength: 2000, nullable: true),
                    PublicCertificateBytes = table.Column<byte[]>(type: "longblob", nullable: true),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySigningCertificates", x => x.Key);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VulnerabilityRecords",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AdvisoryUrl = table.Column<string>(type: "varchar(768)", maxLength: 768, nullable: false),
                    Severity = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VulnerabilityRecords", x => x.Key);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    Authors = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    Description = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    Downloads = table.Column<long>(type: "bigint", nullable: false),
                    HasReadme = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasEmbeddedIcon = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasEmbeddedLicense = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsPrerelease = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    Language = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Listed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LicenseExpression = table.Column<string>(type: "longtext", nullable: true),
                    IsSigned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsTool = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDevelopmentDependency = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MinClientVersion = table.Column<string>(type: "varchar(44)", maxLength: 44, nullable: true),
                    Published = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RequireLicenseAcceptance = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SemVerLevel = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    Title = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IconUrl = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    LicenseUrl = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    ProjectUrl = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    RepositoryUrl = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    RepositoryType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    RepositoryCommit = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    RepositoryCommitDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Tags = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    Origin = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, defaultValue: "Published"),
                    PackageSourceId = table.Column<int>(type: "int", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeprecationReasons = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    DeprecationMessage = table.Column<string>(type: "longtext", maxLength: 4000, nullable: true),
                    DeprecatedAlternatePackageId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    DeprecatedAlternatePackageVersionRange = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "longblob", rowVersion: true, nullable: true)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.ComputedColumn),
                    Version = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OriginalVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PackageVulnerabilities",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PackageId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    VersionRange = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    VulnerabilityKey = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PackageDependencies",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true),
                    VersionRange = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    TargetFramework = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PackageDownloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    PackageKey = table.Column<int>(type: "int", nullable: false),
                    RemoteIp = table.Column<string>(type: "varchar(45)", nullable: true),
                    UserAgentString = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NuGetClient = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    NuGetClientVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    ClientPlatform = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    ClientPlatformVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    User = table.Column<string>(type: "longtext", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetime", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PackageTypes",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true),
                    Version = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TargetFrameworks",
                columns: table => new
                {
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Moniker = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Listed",
                table: "Packages",
                column: "Listed");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_IsPrerelease",
                table: "Packages",
                column: "IsPrerelease");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Published",
                table: "Packages",
                column: "Published");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_SemVerLevel",
                table: "Packages",
                column: "SemVerLevel");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Listed_IsPrerelease",
                table: "Packages",
                columns: new[] { "Listed", "IsPrerelease" });

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_PackageDownloadCounts AS
                SELECT 
                    p.`Key` as PackageKey,
                    p.Id as PackageId,
                    p.Version,
                    COUNT(pd.Id) as DownloadCount
                FROM Packages p
                LEFT JOIN PackageDownloads pd ON p.`Key` = pd.PackageKey
                GROUP BY p.`Key`, p.Id, p.Version
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_LatestPackageVersions AS
                WITH RankedPackages AS (
                    SELECT 
                        p.*,
                        ROW_NUMBER() OVER (PARTITION BY p.Id ORDER BY p.Published DESC) as RowNum
                    FROM Packages p
                    WHERE p.Listed = 1
                )
                SELECT * FROM RankedPackages WHERE RowNum = 1
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_PackageSearchInfo AS
                SELECT 
                    p.`Key`,
                    p.Id,
                    p.Version,
                    p.Description,
                    p.Authors,
                    p.HasEmbeddedIcon,
                    p.HasEmbeddedLicense,
                    p.IconUrl,
                    p.LicenseUrl,
                    p.ProjectUrl,
                    p.Published,
                    p.Summary,
                    p.Tags,
                    p.Title,
                    p.Listed,
                    p.IsPrerelease,
                    p.SemVerLevel,
                    IFNULL(dc.TotalDownloads, 0) as TotalDownloads
                FROM Packages p
                LEFT JOIN (
                    SELECT PackageKey, COUNT(*) as TotalDownloads
                    FROM PackageDownloads
                    GROUP BY PackageKey
                ) dc ON p.`Key` = dc.PackageKey
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_PackageVersionsWithDownloads AS
                SELECT 
                    p.Id,
                    p.Version,
                    p.IsPrerelease,
                    p.Listed,
                    IFNULL(COUNT(pd.Id), 0) as Downloads
                FROM Packages p
                LEFT JOIN PackageDownloads pd ON p.`Key` = pd.PackageKey
                GROUP BY p.Id, p.Version, p.IsPrerelease, p.Listed
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE VIEW vw_PackageWithJsonData AS
                SELECT 
                    p.`Key`,
                    p.Id,
                    p.Version,
                    p.OriginalVersion,
                    p.Authors,
                    p.Description,
                    p.Downloads,
                    p.HasReadme,
                    p.HasEmbeddedIcon,
                    p.HasEmbeddedLicense,
                    p.IsPrerelease,
                    p.ReleaseNotes,
                    p.Language,
                    p.Listed,
                    p.LicenseExpression,
                    p.IsSigned,
                    p.IsTool,
                    p.IsDevelopmentDependency,
                    p.MinClientVersion,
                    p.Published,
                    p.RequireLicenseAcceptance,
                    p.SemVerLevel,
                    p.Summary,
                    p.Title,
                    p.IconUrl,
                    p.LicenseUrl,
                    p.ProjectUrl,
                    p.RepositoryUrl,
                    p.RepositoryType,
                    p.RepositoryCommit,
                    p.RepositoryCommitDate,
                    p.Tags,
                    p.IsDeprecated,
                    p.DeprecationReasons,
                    p.DeprecationMessage,
                    p.DeprecatedAlternatePackageId,
                    p.DeprecatedAlternatePackageVersionRange,
                    p.RowVersion,
                    (
                        SELECT JSON_ARRAYAGG(JSON_OBJECT(
                            'Id', d.Id,
                            'VersionRange', d.VersionRange,
                            'TargetFramework', d.TargetFramework
                        ))
                        FROM PackageDependencies d
                        WHERE d.PackageKey = p.`Key`
                    ) AS DependenciesJson,
                    (
                        SELECT JSON_ARRAYAGG(JSON_OBJECT(
                            'Name', pt.Name,
                            'Version', pt.Version
                        ))
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = p.`Key`
                    ) AS PackageTypesJson,
                    (
                        SELECT JSON_ARRAYAGG(JSON_OBJECT(
                            'Moniker', tf.Moniker
                        ))
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = p.`Key`
                    ) AS TargetFrameworksJson
                FROM Packages p
            ");
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

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageWithJsonData;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageVersionsWithDownloads;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageSearchInfo;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_LatestPackageVersions;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageDownloadCounts;");

            migrationBuilder.DropIndex(
                name: "IX_Packages_Listed_IsPrerelease",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_SemVerLevel",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_Published",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_IsPrerelease",
                table: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Packages_Listed",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "VulnerabilityRecords");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "PackageSources");
        }
    }
}
