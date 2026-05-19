using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageOriginIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Origin",
                table: "Packages",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "Published",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Published");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Origin",
                table: "Packages",
                column: "Origin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Packages_Origin",
                table: "Packages");

            migrationBuilder.AlterColumn<string>(
                name: "Origin",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Published",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldDefaultValue: "Published");
        }
    }
}
