using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddJsonColumnsAndView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create optimized view that dynamically generates JSON columns
            // No physical columns are added - JSON is computed on-the-fly by SQLite
            migrationBuilder.Sql(@"
                CREATE VIEW IF NOT EXISTS vw_PackageWithJsonData AS
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
            // Drop the view
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageWithJsonData");
        }
    }
}
