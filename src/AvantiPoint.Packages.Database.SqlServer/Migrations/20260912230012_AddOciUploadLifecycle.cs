using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddOciUploadLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "OciUploads",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "OciUploads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OciUploads_FeedId_IsCancelled",
                table: "OciUploads",
                columns: new[] { "FeedId", "IsCancelled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OciUploads_FeedId_IsCancelled",
                table: "OciUploads");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "OciUploads");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "OciUploads");
        }
    }
}
