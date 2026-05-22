using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvantiPoint.Packages.Database.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OciTags_FeedId_OciSegment_RepositoryKey_Tag",
                table: "OciTags");

            migrationBuilder.DropIndex(
                name: "IX_OciRepositories_FeedId_OciSegment_Name",
                table: "OciRepositories");

            migrationBuilder.DropIndex(
                name: "IX_OciManifests_FeedId_OciSegment_Digest",
                table: "OciManifests");

            migrationBuilder.DropIndex(
                name: "IX_OciBlobs_FeedId_OciSegment_Digest",
                table: "OciBlobs");

            migrationBuilder.CreateIndex(
                name: "IX_OciTags_FeedId_OciSegment_RepositoryKey_Tag",
                table: "OciTags",
                columns: new[] { "FeedId", "OciSegment", "RepositoryKey", "Tag" },
                unique: true,
                filter: "[OciSegment] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OciRepositories_FeedId_OciSegment_Name",
                table: "OciRepositories",
                columns: new[] { "FeedId", "OciSegment", "Name" },
                unique: true,
                filter: "[OciSegment] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OciManifests_FeedId_OciSegment_Digest",
                table: "OciManifests",
                columns: new[] { "FeedId", "OciSegment", "Digest" },
                unique: true,
                filter: "[OciSegment] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OciBlobs_FeedId_OciSegment_Digest",
                table: "OciBlobs",
                columns: new[] { "FeedId", "OciSegment", "Digest" },
                unique: true,
                filter: "[OciSegment] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OciTags_FeedId_OciSegment_RepositoryKey_Tag",
                table: "OciTags");

            migrationBuilder.DropIndex(
                name: "IX_OciRepositories_FeedId_OciSegment_Name",
                table: "OciRepositories");

            migrationBuilder.DropIndex(
                name: "IX_OciManifests_FeedId_OciSegment_Digest",
                table: "OciManifests");

            migrationBuilder.DropIndex(
                name: "IX_OciBlobs_FeedId_OciSegment_Digest",
                table: "OciBlobs");

            migrationBuilder.CreateIndex(
                name: "IX_OciTags_FeedId_OciSegment_RepositoryKey_Tag",
                table: "OciTags",
                columns: new[] { "FeedId", "OciSegment", "RepositoryKey", "Tag" },
                unique: true,
                filter: "[FeedId] IS NOT NULL AND [OciSegment] IS NOT NULL AND [Tag] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OciRepositories_FeedId_OciSegment_Name",
                table: "OciRepositories",
                columns: new[] { "FeedId", "OciSegment", "Name" },
                unique: true,
                filter: "[FeedId] IS NOT NULL AND [OciSegment] IS NOT NULL AND [Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OciManifests_FeedId_OciSegment_Digest",
                table: "OciManifests",
                columns: new[] { "FeedId", "OciSegment", "Digest" },
                unique: true,
                filter: "[FeedId] IS NOT NULL AND [OciSegment] IS NOT NULL AND [Digest] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OciBlobs_FeedId_OciSegment_Digest",
                table: "OciBlobs",
                columns: new[] { "FeedId", "OciSegment", "Digest" },
                unique: true,
                filter: "[FeedId] IS NOT NULL AND [OciSegment] IS NOT NULL AND [Digest] IS NOT NULL");
        }
    }
}
