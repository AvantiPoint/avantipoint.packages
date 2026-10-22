using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Host.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedApiTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllFeeds",
                table: "HostApiTokens",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "HostApiTokenFeeds",
                columns: table => new
                {
                    HostApiTokenId = table.Column<int>(type: "INTEGER", nullable: false),
                    FeedId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SurfaceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
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
                name: "AllFeeds",
                table: "HostApiTokens");
        }
    }
}
