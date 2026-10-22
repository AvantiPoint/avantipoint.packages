using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Host.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedApiTokensAndSessionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "HostUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllFeeds",
                table: "HostApiTokens",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "HostApiTokenFeeds",
                columns: table => new
                {
                    HostApiTokenId = table.Column<int>(type: "integer", nullable: false),
                    FeedId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SurfaceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostApiTokenFeeds", x => new { x.HostApiTokenId, x.FeedId, x.SurfaceId });
                    table.ForeignKey(
                        name: "FK_HostApiTokenFeeds_HostApiTokens_HostApiTokenId",
                        column: x => x.HostApiTokenId,
                        principalTable: "HostApiTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostApiTokenFeeds");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "HostUsers");

            migrationBuilder.DropColumn(
                name: "AllFeeds",
                table: "HostApiTokens");
        }
    }
}
