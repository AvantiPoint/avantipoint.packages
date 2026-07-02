using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
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
                type: "nvarchar(64)",
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
