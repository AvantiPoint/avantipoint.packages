using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add indexes on frequently filtered columns
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

            // Composite index for common query patterns
            migrationBuilder.CreateIndex(
                name: "IX_Packages_Listed_IsPrerelease",
                table: "Packages",
                columns: new[] { "Listed", "IsPrerelease" });

            // Create a view for package download counts to avoid repeated aggregation
            migrationBuilder.Sql(@"
                CREATE VIEW vw_PackageDownloadCounts AS
                SELECT 
                    p.[Key] as PackageKey,
                    p.Id as PackageId,
                    p.Version,
                    COUNT(pd.Id) as DownloadCount
                FROM Packages p
                LEFT JOIN PackageDownloads pd ON p.[Key] = pd.PackageKey
                GROUP BY p.[Key], p.Id, p.Version
            ");

            // Create a view for latest package versions to optimize search queries
            migrationBuilder.Sql(@"
                CREATE VIEW vw_LatestPackageVersions AS
                SELECT p.*
                FROM Packages p
                WHERE p.Listed = 1
                AND p.Published = (
                    SELECT MAX(p2.Published)
                    FROM Packages p2
                    WHERE p2.Id = p.Id AND p2.Listed = 1
                )
            ");

            // Create a view combining package info with download counts
            migrationBuilder.Sql(@"
                CREATE VIEW vw_PackageSearchInfo AS
                SELECT 
                    p.[Key],
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
                    COALESCE(dc.TotalDownloads, 0) as TotalDownloads
                FROM Packages p
                LEFT JOIN (
                    SELECT PackageKey, COUNT(*) as TotalDownloads
                    FROM PackageDownloads
                    GROUP BY PackageKey
                ) dc ON p.[Key] = dc.PackageKey
            ");

            // Create a view for package versions with download counts
            migrationBuilder.Sql(@"
                CREATE VIEW vw_PackageVersionsWithDownloads AS
                SELECT 
                    p.Id,
                    p.Version,
                    p.IsPrerelease,
                    p.Listed,
                    COALESCE(COUNT(pd.Id), 0) as Downloads
                FROM Packages p
                LEFT JOIN PackageDownloads pd ON p.[Key] = pd.PackageKey
                GROUP BY p.Id, p.Version, p.IsPrerelease, p.Listed
            ");

            // Create optimized view that dynamically generates JSON columns
            // No physical columns are added - JSON is computed on-the-fly by SQLite
            migrationBuilder.Sql(@"
                CREATE VIEW vw_PackageWithJsonData AS
                SELECT 
                    p.[Key],
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
                    -- Dynamically generate JSON columns from relationship tables
                    (
                        SELECT json_group_array(
                            json_object(
                                'Id', d.Id,
                                'VersionRange', d.VersionRange,
                                'TargetFramework', d.TargetFramework
                            )
                        )
                        FROM PackageDependencies d
                        WHERE d.PackageKey = p.[Key]
                    ) AS DependenciesJson,
                    (
                        SELECT json_group_array(
                            json_object(
                                'Name', pt.Name,
                                'Version', pt.Version
                            )
                        )
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = p.[Key]
                    ) AS PackageTypesJson,
                    (
                        SELECT json_group_array(
                            json_object(
                                'Moniker', tf.Moniker
                            )
                        )
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = p.[Key]
                    ) AS TargetFrameworksJson
                FROM Packages p
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views in reverse order
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageWithJsonData");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageVersionsWithDownloads");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageSearchInfo");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_LatestPackageVersions");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageDownloadCounts");

            // Drop indexes
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
        }
    }
}
