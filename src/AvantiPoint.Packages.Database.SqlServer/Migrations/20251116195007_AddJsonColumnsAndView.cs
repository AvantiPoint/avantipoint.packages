using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonColumnsAndView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create optimized view that dynamically generates JSON columns
            // No physical columns are added - JSON is computed on-the-fly by SQL Server
            migrationBuilder.Sql(@"
                CREATE OR ALTER VIEW [dbo].[vw_PackageWithJsonData] AS
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
                        SELECT 
                            d.Id,
                            d.VersionRange,
                            d.TargetFramework
                        FROM PackageDependencies d
                        WHERE d.PackageKey = p.[Key]
                        FOR JSON PATH
                    ) AS DependenciesJson,
                    (
                        SELECT 
                            pt.Name,
                            pt.Version
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = p.[Key]
                        FOR JSON PATH
                    ) AS PackageTypesJson,
                    (
                        SELECT 
                            tf.Moniker
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = p.[Key]
                        FOR JSON PATH
                    ) AS TargetFrameworksJson
                FROM Packages p
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the view
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_PackageWithJsonData]");
        }
    }
}
