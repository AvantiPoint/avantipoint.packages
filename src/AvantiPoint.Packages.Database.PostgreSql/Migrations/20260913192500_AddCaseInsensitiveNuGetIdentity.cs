using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AvantiPoint.Packages.Database.PostgreSql.Migrations;

// Expression indexes cannot be represented by HasIndex in the EF model snapshot.
[DbContext(typeof(PostgreSqlContext))]
[Migration("20260913192500_AddCaseInsensitiveNuGetIdentity")]
public sealed class AddCaseInsensitiveNuGetIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE UNIQUE INDEX "IX_Packages_CaseInsensitiveIdentity"
        ON "Packages" (lower("Id"), "FeedId", lower("Version"));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP INDEX \"IX_Packages_CaseInsensitiveIdentity\";");
}
