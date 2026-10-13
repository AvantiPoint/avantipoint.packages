using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.PostgreSql.Migrations
{
    public partial class AddNativeUploadCallbackReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UploadCallbackCompleted",
                table: "NativeArtifacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(
                name: "UploadCallbackLeaseUntilUtc",
                table: "NativeArtifacts",
                type: "timestamp with time zone",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "UploadCallbackOwner",
                table: "NativeArtifacts",
                type: "character varying(32)",
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
