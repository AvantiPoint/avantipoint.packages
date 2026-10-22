using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AvantiPoint.Packages.Database.PostgreSql.Migrations;

// Expression indexes cannot be represented by HasIndex in the EF model snapshot.
[DbContext(typeof(PostgreSqlContext))]
[Migration("20260913192500_AddCaseInsensitiveNuGetIdentity")]
public sealed class AddCaseInsensitiveNuGetIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Keep concurrent package writes from changing the preflight result before index creation.
        LOCK TABLE "Packages" IN SHARE MODE;
        DO $preflight$
        DECLARE
            collision_count bigint;
            collision_report text;
        BEGIN
            SELECT count(*) INTO collision_count
            FROM (
                SELECT 1 FROM "Packages"
                GROUP BY lower("Id"), "FeedId", lower("Version")
                HAVING count(*) > 1
            ) collisions;
            IF collision_count > 0 THEN
                SELECT jsonb_agg(to_jsonb(collisions))::text INTO collision_report
                FROM (
                    SELECT lower("Id") AS package_id, "FeedId" AS feed_id,
                           lower("Version") AS version, array_agg("Key" ORDER BY "Key") AS package_keys
                    FROM "Packages"
                    GROUP BY lower("Id"), "FeedId", lower("Version")
                    HAVING count(*) > 1
                    ORDER BY "FeedId", lower("Id"), lower("Version")
                    LIMIT 20
                ) collisions;
                RAISE EXCEPTION USING
                    ERRCODE = '23505',
                    MESSAGE = format('NuGet identity upgrade blocked: %s case-insensitive collision group(s). No package rows were changed.', collision_count),
                    DETAIL = collision_report,
                    HINT = 'Preserve all rows and package contents. Review the collision keys using the documented upgrade preflight; resolve identities through the approved operator process, then retry. Do not automatically merge or delete packages.';
            END IF;
        END $preflight$;
        CREATE UNIQUE INDEX "IX_Packages_CaseInsensitiveIdentity"
        ON "Packages" (lower("Id"), "FeedId", lower("Version"));
        """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP INDEX \"IX_Packages_CaseInsensitiveIdentity\";");
}
