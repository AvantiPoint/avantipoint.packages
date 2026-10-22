using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.MySql.Migrations
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
                    FeedId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    Segment = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciMutationGates", x => new { x.FeedId, x.Segment });
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OciMutationGates");
        }
    }
}
