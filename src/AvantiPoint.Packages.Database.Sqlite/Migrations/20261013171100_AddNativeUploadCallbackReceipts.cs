using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    public partial class AddNativeUploadCallbackReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UploadCallbackCompleted",
                table: "NativeArtifacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(
                name: "UploadCallbackLeaseUntilUtc",
                table: "NativeArtifacts",
                type: "TEXT",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "UploadCallbackOwner",
                table: "NativeArtifacts",
                type: "TEXT",
                maxLength: 32,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UploadCallbackCompleted", table: "NativeArtifacts");
            migrationBuilder.DropColumn(name: "UploadCallbackLeaseUntilUtc", table: "NativeArtifacts");
            migrationBuilder.DropColumn(name: "UploadCallbackOwner", table: "NativeArtifacts");
        }
    }
}
