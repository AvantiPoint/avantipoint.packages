using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
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
                type: "varbinary(max)",
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
