using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddOciMutationGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OciMutationGates",
                columns: table => new
                {
                    FeedId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Segment = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Revision = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciMutationGates", x => new { x.FeedId, x.Segment });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OciMutationGates");
        }
    }
}
