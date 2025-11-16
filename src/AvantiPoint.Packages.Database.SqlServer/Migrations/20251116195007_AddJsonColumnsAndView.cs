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
            migrationBuilder.AddColumn<string>(
                name: "DependenciesJson",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageTypesJson",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetFrameworksJson",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: true);

            // Populate JSON columns from existing relationship tables
            migrationBuilder.Sql(@"
                UPDATE p
                SET p.DependenciesJson = (
                    SELECT 
                        d.Id,
                        d.VersionRange,
                        d.TargetFramework
                    FROM PackageDependencies d
                    WHERE d.PackageKey = p.[Key]
                    FOR JSON PATH
                ),
                p.PackageTypesJson = (
                    SELECT 
                        pt.Name,
                        pt.Version
                    FROM PackageTypes pt
                    WHERE pt.PackageKey = p.[Key]
                    FOR JSON PATH
                ),
                p.TargetFrameworksJson = (
                    SELECT 
                        tf.Moniker
                    FROM TargetFrameworks tf
                    WHERE tf.PackageKey = p.[Key]
                    FOR JSON PATH
                )
                FROM Packages p
            ");

            // Create triggers to keep JSON columns in sync with relationship tables
            migrationBuilder.Sql(@"
                CREATE OR ALTER TRIGGER trg_PackageDependencies_UpdateJson
                ON PackageDependencies
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    -- Update for affected packages from inserted rows
                    UPDATE p
                    SET p.DependenciesJson = (
                        SELECT 
                            d.Id,
                            d.VersionRange,
                            d.TargetFramework
                        FROM PackageDependencies d
                        WHERE d.PackageKey = p.[Key]
                        FOR JSON PATH
                    )
                    FROM Packages p
                    WHERE p.[Key] IN (SELECT DISTINCT PackageKey FROM inserted)
                    
                    -- Update for affected packages from deleted rows
                    UPDATE p
                    SET p.DependenciesJson = (
                        SELECT 
                            d.Id,
                            d.VersionRange,
                            d.TargetFramework
                        FROM PackageDependencies d
                        WHERE d.PackageKey = p.[Key]
                        FOR JSON PATH
                    )
                    FROM Packages p
                    WHERE p.[Key] IN (SELECT DISTINCT PackageKey FROM deleted)
                END
            ");

            migrationBuilder.Sql(@"
                CREATE OR ALTER TRIGGER trg_PackageTypes_UpdateJson
                ON PackageTypes
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    -- Update for affected packages from inserted rows
                    UPDATE p
                    SET p.PackageTypesJson = (
                        SELECT 
                            pt.Name,
                            pt.Version
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = p.[Key]
                        FOR JSON PATH
                    )
                    FROM Packages p
                    WHERE p.[Key] IN (SELECT DISTINCT PackageKey FROM inserted)
                    
                    -- Update for affected packages from deleted rows
                    UPDATE p
                    SET p.PackageTypesJson = (
                        SELECT 
                            pt.Name,
                            pt.Version
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = p.[Key]
                        FOR JSON PATH
                    )
                    FROM Packages p
                    WHERE p.[Key] IN (SELECT DISTINCT PackageKey FROM deleted)
                END
            ");

            migrationBuilder.Sql(@"
                CREATE OR ALTER TRIGGER trg_TargetFrameworks_UpdateJson
                ON TargetFrameworks
                AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    -- Update for affected packages from inserted rows
                    UPDATE p
                    SET p.TargetFrameworksJson = (
                        SELECT 
                            tf.Moniker
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = p.[Key]
                        FOR JSON PATH
                    )
                    FROM Packages p
                    WHERE p.[Key] IN (SELECT DISTINCT PackageKey FROM inserted)
                    
                    -- Update for affected packages from deleted rows
                    UPDATE p
                    SET p.TargetFrameworksJson = (
                        SELECT 
                            tf.Moniker
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = p.[Key]
                        FOR JSON PATH
                    )
                    FROM Packages p
                    WHERE p.[Key] IN (SELECT DISTINCT PackageKey FROM deleted)
                END
            ");

            // Create optimized view that uses JSON columns
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
                    p.DependenciesJson,
                    p.PackageTypesJson,
                    p.TargetFrameworksJson,
                    p.RowVersion
                FROM Packages p
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the view
            migrationBuilder.Sql("DROP VIEW IF EXISTS [dbo].[vw_PackageWithJsonData]");

            // Drop triggers
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageDependencies_UpdateJson");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageTypes_UpdateJson");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_TargetFrameworks_UpdateJson");

            migrationBuilder.DropColumn(
                name: "DependenciesJson",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "PackageTypesJson",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "TargetFrameworksJson",
                table: "Packages");
        }
    }
}
