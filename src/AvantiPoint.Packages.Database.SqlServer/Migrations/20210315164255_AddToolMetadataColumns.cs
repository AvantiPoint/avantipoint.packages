using Microsoft.EntityFrameworkCore.Migrations;

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    public partial class AddToolMetadataColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDevelopmentDependency",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSigned",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTool",
                table: "Packages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LicenseExpression",
                table: "Packages",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDevelopmentDependency",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "IsSigned",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "IsTool",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "LicenseExpression",
                table: "Packages");
        }
    }
}
