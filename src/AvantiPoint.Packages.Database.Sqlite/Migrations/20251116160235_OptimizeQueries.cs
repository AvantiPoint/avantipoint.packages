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

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
