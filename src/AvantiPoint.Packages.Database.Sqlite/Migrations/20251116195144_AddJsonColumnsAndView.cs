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
            migrationBuilder.AddColumn<string>(
                name: "DependenciesJson",
                table: "Packages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackageTypesJson",
                table: "Packages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetFrameworksJson",
                table: "Packages",
                type: "TEXT",
                nullable: true);

            // Populate JSON columns from existing relationship tables using SQLite JSON functions
            migrationBuilder.Sql(@"
                UPDATE Packages
                SET DependenciesJson = (
                    SELECT json_group_array(
                        json_object(
                            'Id', d.Id,
                            'VersionRange', d.VersionRange,
                            'TargetFramework', d.TargetFramework
                        )
                    )
                    FROM PackageDependencies d
                    WHERE d.PackageKey = Packages.[Key]
                ),
                PackageTypesJson = (
                    SELECT json_group_array(
                        json_object(
                            'Name', pt.Name,
                            'Version', pt.Version
                        )
                    )
                    FROM PackageTypes pt
                    WHERE pt.PackageKey = Packages.[Key]
                ),
                TargetFrameworksJson = (
                    SELECT json_group_array(
                        json_object(
                            'Moniker', tf.Moniker
                        )
                    )
                    FROM TargetFrameworks tf
                    WHERE tf.PackageKey = Packages.[Key]
                )
            ");

            // Create triggers to keep JSON columns in sync with relationship tables
            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_PackageDependencies_UpdateJson_Insert
                AFTER INSERT ON PackageDependencies
                BEGIN
                    UPDATE Packages
                    SET DependenciesJson = (
                        SELECT json_group_array(
                            json_object(
                                'Id', d.Id,
                                'VersionRange', d.VersionRange,
                                'TargetFramework', d.TargetFramework
                            )
                        )
                        FROM PackageDependencies d
                        WHERE d.PackageKey = NEW.PackageKey
                    )
                    WHERE [Key] = NEW.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_PackageDependencies_UpdateJson_Update
                AFTER UPDATE ON PackageDependencies
                BEGIN
                    UPDATE Packages
                    SET DependenciesJson = (
                        SELECT json_group_array(
                            json_object(
                                'Id', d.Id,
                                'VersionRange', d.VersionRange,
                                'TargetFramework', d.TargetFramework
                            )
                        )
                        FROM PackageDependencies d
                        WHERE d.PackageKey = NEW.PackageKey
                    )
                    WHERE [Key] = NEW.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_PackageDependencies_UpdateJson_Delete
                AFTER DELETE ON PackageDependencies
                BEGIN
                    UPDATE Packages
                    SET DependenciesJson = (
                        SELECT json_group_array(
                            json_object(
                                'Id', d.Id,
                                'VersionRange', d.VersionRange,
                                'TargetFramework', d.TargetFramework
                            )
                        )
                        FROM PackageDependencies d
                        WHERE d.PackageKey = OLD.PackageKey
                    )
                    WHERE [Key] = OLD.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_PackageTypes_UpdateJson_Insert
                AFTER INSERT ON PackageTypes
                BEGIN
                    UPDATE Packages
                    SET PackageTypesJson = (
                        SELECT json_group_array(
                            json_object(
                                'Name', pt.Name,
                                'Version', pt.Version
                            )
                        )
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = NEW.PackageKey
                    )
                    WHERE [Key] = NEW.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_PackageTypes_UpdateJson_Update
                AFTER UPDATE ON PackageTypes
                BEGIN
                    UPDATE Packages
                    SET PackageTypesJson = (
                        SELECT json_group_array(
                            json_object(
                                'Name', pt.Name,
                                'Version', pt.Version
                            )
                        )
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = NEW.PackageKey
                    )
                    WHERE [Key] = NEW.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_PackageTypes_UpdateJson_Delete
                AFTER DELETE ON PackageTypes
                BEGIN
                    UPDATE Packages
                    SET PackageTypesJson = (
                        SELECT json_group_array(
                            json_object(
                                'Name', pt.Name,
                                'Version', pt.Version
                            )
                        )
                        FROM PackageTypes pt
                        WHERE pt.PackageKey = OLD.PackageKey
                    )
                    WHERE [Key] = OLD.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_TargetFrameworks_UpdateJson_Insert
                AFTER INSERT ON TargetFrameworks
                BEGIN
                    UPDATE Packages
                    SET TargetFrameworksJson = (
                        SELECT json_group_array(
                            json_object(
                                'Moniker', tf.Moniker
                            )
                        )
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = NEW.PackageKey
                    )
                    WHERE [Key] = NEW.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_TargetFrameworks_UpdateJson_Update
                AFTER UPDATE ON TargetFrameworks
                BEGIN
                    UPDATE Packages
                    SET TargetFrameworksJson = (
                        SELECT json_group_array(
                            json_object(
                                'Moniker', tf.Moniker
                            )
                        )
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = NEW.PackageKey
                    )
                    WHERE [Key] = NEW.PackageKey;
                END
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER IF NOT EXISTS trg_TargetFrameworks_UpdateJson_Delete
                AFTER DELETE ON TargetFrameworks
                BEGIN
                    UPDATE Packages
                    SET TargetFrameworksJson = (
                        SELECT json_group_array(
                            json_object(
                                'Moniker', tf.Moniker
                            )
                        )
                        FROM TargetFrameworks tf
                        WHERE tf.PackageKey = OLD.PackageKey
                    )
                    WHERE [Key] = OLD.PackageKey;
                END
            ");

            // Create optimized view that uses JSON columns
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
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_PackageWithJsonData");

            // Drop triggers
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageDependencies_UpdateJson_Insert");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageDependencies_UpdateJson_Update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageDependencies_UpdateJson_Delete");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageTypes_UpdateJson_Insert");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageTypes_UpdateJson_Update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_PackageTypes_UpdateJson_Delete");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_TargetFrameworks_UpdateJson_Insert");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_TargetFrameworks_UpdateJson_Update");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_TargetFrameworks_UpdateJson_Delete");

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
