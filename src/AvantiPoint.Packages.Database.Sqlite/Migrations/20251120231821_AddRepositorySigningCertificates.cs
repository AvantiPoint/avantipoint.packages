using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositorySigningCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RepositorySigningCertificates",
                columns: table => new
                {
                    Key = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    HashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    NotBefore = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotAfter = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FirstUsed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ContentUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySigningCertificates", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_Fingerprint_HashAlgorithm",
                table: "RepositorySigningCertificates",
                columns: new[] { "Fingerprint", "HashAlgorithm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_FirstUsed",
                table: "RepositorySigningCertificates",
                column: "FirstUsed");

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_IsActive_NotBefore_NotAfter",
                table: "RepositorySigningCertificates",
                columns: new[] { "IsActive", "NotBefore", "NotAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySigningCertificates_LastUsed",
                table: "RepositorySigningCertificates",
                column: "LastUsed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositorySigningCertificates");
        }
    }
}
