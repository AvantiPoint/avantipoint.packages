using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IndexedWith",
                table: "Packages",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SearchIndexStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    LastReconcileCompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReconcileInProgress = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_Id_IndexedWith",
                table: "Packages",
                columns: new[] { "Id", "IndexedWith" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SearchIndexStates");

            migrationBuilder.DropIndex(
                name: "IX_Packages_Id_IndexedWith",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "IndexedWith",
                table: "Packages");
        }
    }
}
