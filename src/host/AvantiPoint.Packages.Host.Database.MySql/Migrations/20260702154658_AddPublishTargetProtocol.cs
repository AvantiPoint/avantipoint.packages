using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Host.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishTargetProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "HostPublishTargets",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NuGet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "HostPublishTargets");
        }
    }
}
