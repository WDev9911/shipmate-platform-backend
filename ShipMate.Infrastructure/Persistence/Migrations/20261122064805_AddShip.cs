using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ship_ownership_token",
                table: "workspaces",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Workspaces created before SHIP get their own token too, in the same form the application creates
            // ("shipmate-" and 32 hex characters). md5 works on every PostgreSQL version; the token only has to be
            // unique to its workspace, not secret: the team publishes it on its own site.
            migrationBuilder.Sql("""
                UPDATE workspaces
                SET ship_ownership_token = 'shipmate-' || md5(id::text || random()::text || clock_timestamp()::text)
                WHERE ship_ownership_token = '';
                """);

            migrationBuilder.CreateTable(
                name: "ship_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_version = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    failure_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ping_status_code = table.Column<int>(type: "integer", nullable: true),
                    ping_error = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ssl_valid = table.Column<bool>(type: "boolean", nullable: false),
                    ssl_error = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ssl_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ownership_verified = table.Column<bool>(type: "boolean", nullable: false),
                    ownership_error = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ship_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_ship_attempts_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ship_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_version = table.Column<int>(type: "integer", nullable: false),
                    ship_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    shipped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    shipped_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_to_ship_seconds = table.Column<long>(type: "bigint", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    score_formula_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score_breakdown = table.Column<string>(type: "jsonb", nullable: false),
                    verification_checklist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verification_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    audit_anchor_sequence = table.Column<long>(type: "bigint", nullable: false),
                    audit_anchor_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    public_slug = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ship_cards", x => x.id);
                    table.ForeignKey(
                        name: "FK_ship_cards_ship_attempts_ship_attempt_id",
                        column: x => x.ship_attempt_id,
                        principalTable: "ship_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ship_cards_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ship_attempts_workspace_id_created_at",
                table: "ship_attempts",
                columns: new[] { "workspace_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ship_cards_public_slug",
                table: "ship_cards",
                column: "public_slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ship_cards_ship_attempt_id",
                table: "ship_cards",
                column: "ship_attempt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ship_cards_workspace_id_snapshot_version",
                table: "ship_cards",
                columns: new[] { "workspace_id", "snapshot_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ship_cards");

            migrationBuilder.DropTable(
                name: "ship_attempts");

            migrationBuilder.DropColumn(
                name: "ship_ownership_token",
                table: "workspaces");
        }
    }
}
