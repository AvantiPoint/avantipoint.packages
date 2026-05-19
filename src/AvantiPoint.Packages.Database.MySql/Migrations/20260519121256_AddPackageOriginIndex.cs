using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.MySql.Migrations
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
                type: "varchar(255)",
                nullable: false,
                defaultValue: "Published",
                oldClrType: typeof(string),
                oldType: "longtext",
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
                type: "longtext",
                nullable: false,
                defaultValue: "Published",
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldDefaultValue: "Published");
        }
    }
}
