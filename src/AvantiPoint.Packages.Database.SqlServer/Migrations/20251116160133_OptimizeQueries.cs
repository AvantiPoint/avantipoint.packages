using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
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
                CREATE VIEW [dbo].[vw_PackageDownloadCounts] AS
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
                CREATE VIEW [dbo].[vw_LatestPackageVersions] AS
                WITH RankedPackages AS (
                    SELECT 
                        p.*,
                        ROW_NUMBER() OVER (PARTITION BY p.Id ORDER BY p.Published DESC) as RowNum
                    FROM Packages p
                    WHERE p.Listed = 1
                )
                SELECT * FROM RankedPackages WHERE RowNum = 1
            ");

            // Create a view combining package info with download counts
            migrationBuilder.Sql(@"
                CREATE VIEW [dbo].[vw_PackageSearchInfo] AS
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
                    ISNULL(dc.TotalDownloads, 0) as TotalDownloads
                FROM Packages p
                LEFT JOIN (
                    SELECT PackageKey, COUNT(*) as TotalDownloads
                    FROM PackageDownloads
                    GROUP BY PackageKey
                ) dc ON p.[Key] = dc.PackageKey
            ");

            // Create a view for package versions with download counts
            migrationBuilder.Sql(@"
                CREATE VIEW [dbo].[vw_PackageVersionsWithDownloads] AS
                SELECT 
                    p.Id,
                    p.Version,
                    p.IsPrerelease,
                    p.Listed,
                    ISNULL(COUNT(pd.Id), 0) as Downloads
                FROM Packages p
                LEFT JOIN PackageDownloads pd ON p.[Key] = pd.PackageKey
                GROUP BY p.Id, p.Version, p.IsPrerelease, p.Listed
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views in reverse order
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_PackageVersionsWithDownloads]");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_PackageSearchInfo]");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_LatestPackageVersions]");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_PackageDownloadCounts]");

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
