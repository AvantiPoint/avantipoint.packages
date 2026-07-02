using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "PackageSources",
                type: "TEXT",
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
