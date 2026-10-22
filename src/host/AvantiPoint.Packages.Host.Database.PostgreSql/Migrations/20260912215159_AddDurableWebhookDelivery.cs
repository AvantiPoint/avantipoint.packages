using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AvantiPoint.Packages.Host.Database.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableWebhookDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostWebhookCheckpoints",
                columns: table => new
                {
                    SubscriptionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastQueuedEventId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostWebhookCheckpoints", x => x.SubscriptionId);
                });

            migrationBuilder.CreateTable(
                name: "HostWebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LeaseExpiresUnixMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostWebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostWebhookDeliveries_HostAuditEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "HostAuditEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostWebhookDeliveries_EventId",
                table: "HostWebhookDeliveries",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_HostWebhookDeliveries_SubscriptionId_EventId",
                table: "HostWebhookDeliveries",
                columns: new[] { "SubscriptionId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HostWebhookDeliveries_SubscriptionId_Status_NextAttemptUnix~",
                table: "HostWebhookDeliveries",
                columns: new[] { "SubscriptionId", "Status", "NextAttemptUnixMilliseconds" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostWebhookCheckpoints");

            migrationBuilder.DropTable(
                name: "HostWebhookDeliveries");
        }
    }
}
