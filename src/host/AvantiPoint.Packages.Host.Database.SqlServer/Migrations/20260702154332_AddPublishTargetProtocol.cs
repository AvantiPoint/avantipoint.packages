using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Host.Database.SqlServer.Migrations
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
                type: "nvarchar(max)",
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
