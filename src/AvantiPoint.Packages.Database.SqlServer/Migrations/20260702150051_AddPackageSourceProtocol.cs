using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageSourceProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "PackageSources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "PackageSources",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "NuGet");

            migrationBuilder.CreateIndex(
                name: "IX_PackageSources_Protocol",
                table: "PackageSources",
                column: "Protocol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackageSources_Protocol",
                table: "PackageSources");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "PackageSources");

            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "PackageSources");
        }
    }
}
