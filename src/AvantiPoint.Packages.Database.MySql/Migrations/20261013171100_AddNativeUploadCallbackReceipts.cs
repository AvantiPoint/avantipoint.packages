using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.MySql.Migrations
{
    public partial class AddNativeUploadCallbackReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UploadCallbackCompleted",
                table: "NativeArtifacts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(
                name: "UploadCallbackLeaseUntilUtc",
                table: "NativeArtifacts",
                type: "datetime(6)",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "UploadCallbackOwner",
                table: "NativeArtifacts",
                type: "varchar(32)",
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
