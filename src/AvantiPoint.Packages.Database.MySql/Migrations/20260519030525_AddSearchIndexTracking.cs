using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AdvisoryUrl",
                table: "VulnerabilityRecords",
                type: "varchar(768)",
                maxLength: 768,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldMaxLength: 4000);

            migrationBuilder.AddColumn<string>(
                name: "IndexedWith",
                table: "Packages",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SearchIndexStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    LastReconcileCompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReconcileInProgress = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexStates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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

            migrationBuilder.AlterColumn<string>(
                name: "AdvisoryUrl",
                table: "VulnerabilityRecords",
                type: "longtext",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(768)",
                oldMaxLength: 768);
        }
    }
}
