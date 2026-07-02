using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageSourceSurface : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageSources_Protocol",
                table: "PackageSources");

            migrationBuilder.AddColumn<string>(
                name: "Surface",
                table: "PackageSources",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageSources_Protocol_Surface",
                table: "PackageSources",
                columns: new[] { "Protocol", "Surface" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageSources_Protocol_Surface",
                table: "PackageSources");

            migrationBuilder.DropColumn(
                name: "Surface",
                table: "PackageSources");

            migrationBuilder.CreateIndex(
                name: "IX_PackageSources_Protocol",
                table: "PackageSources",
                column: "Protocol");
        }
    }
}
