using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicCertificateBytesToRepositorySigningCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "PublicCertificateBytes",
                table: "RepositorySigningCertificates",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicCertificateBytes",
                table: "RepositorySigningCertificates");
        }
    }
}
