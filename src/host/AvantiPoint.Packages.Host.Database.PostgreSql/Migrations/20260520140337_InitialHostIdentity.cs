using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AvantiPoint.Packages.Host.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialHostIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostAccessSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequireNewUserApproval = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostAccessSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HostPackageGroups",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostPackageGroups", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "HostPublishTargets",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    PublishEndpoint = table.Column<string>(type: "text", nullable: false),
                    ApiToken = table.Column<string>(type: "text", nullable: false),
                    Legacy = table.Column<bool>(type: "boolean", nullable: false),
                    AddedBy = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostPublishTargets", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "HostUsers",
                columns: table => new
                {
                    Email = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    CanPublish = table.Column<bool>(type: "boolean", nullable: false),
                    CanConsume = table.Column<bool>(type: "boolean", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ExternalProvider = table.Column<int>(type: "integer", nullable: false),
                    ExternalSubjectId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostUsers", x => x.Email);
                });

            migrationBuilder.CreateTable(
                name: "HostPackageGroupMembers",
                columns: table => new
                {
                    PackageGroupName = table.Column<string>(type: "text", nullable: false),
                    PackageId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostPackageGroupMembers", x => new { x.PackageGroupName, x.PackageId });
                    table.ForeignKey(
                        name: "FK_HostPackageGroupMembers_HostPackageGroups_PackageGroupName",
                        column: x => x.PackageGroupName,
                        principalTable: "HostPackageGroups",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostPackageGroupSyndications",
                columns: table => new
                {
                    PackageGroupName = table.Column<string>(type: "text", nullable: false),
                    PublishTargetName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostPackageGroupSyndications", x => new { x.PackageGroupName, x.PublishTargetName });
                    table.ForeignKey(
                        name: "FK_HostPackageGroupSyndications_HostPackageGroups_PackageGroup~",
                        column: x => x.PackageGroupName,
                        principalTable: "HostPackageGroups",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HostPackageGroupSyndications_HostPublishTargets_PublishTarg~",
                        column: x => x.PublishTargetName,
                        principalTable: "HostPublishTargets",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostApiTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenPrefix = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    UserEmail = table.Column<string>(type: "text", nullable: false),
                    Scopes = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemToken = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostApiTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostApiTokens_HostUsers_UserEmail",
                        column: x => x.UserEmail,
                        principalTable: "HostUsers",
                        principalColumn: "Email",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HostTokenNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HostApiTokenId = table.Column<int>(type: "integer", nullable: false),
                    NotificationType = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostTokenNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostTokenNotifications_HostApiTokens_HostApiTokenId",
                        column: x => x.HostApiTokenId,
                        principalTable: "HostApiTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostApiTokens_TokenHash",
                table: "HostApiTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostApiTokens_TokenPrefix",
                table: "HostApiTokens",
                column: "TokenPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_HostApiTokens_UserEmail",
                table: "HostApiTokens",
                column: "UserEmail");

            migrationBuilder.CreateIndex(
                name: "IX_HostPackageGroupSyndications_PublishTargetName",
                table: "HostPackageGroupSyndications",
                column: "PublishTargetName");

            migrationBuilder.CreateIndex(
                name: "IX_HostTokenNotifications_HostApiTokenId",
                table: "HostTokenNotifications",
                column: "HostApiTokenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostAccessSettings");

            migrationBuilder.DropTable(
                name: "HostPackageGroupMembers");

            migrationBuilder.DropTable(
                name: "HostPackageGroupSyndications");

            migrationBuilder.DropTable(
                name: "HostTokenNotifications");

            migrationBuilder.DropTable(
                name: "HostPackageGroups");

            migrationBuilder.DropTable(
                name: "HostPublishTargets");

            migrationBuilder.DropTable(
                name: "HostApiTokens");

            migrationBuilder.DropTable(
                name: "HostUsers");
        }
    }
}
