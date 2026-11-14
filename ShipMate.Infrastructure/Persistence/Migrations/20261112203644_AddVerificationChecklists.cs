using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationChecklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "verification_checklists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mvp_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_version = table.Column<int>(type: "integer", nullable: false),
                    all_items_checked = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_checklists", x => x.id);
                    table.ForeignKey(
                        name: "FK_verification_checklists_mvp_snapshots_mvp_snapshot_id",
                        column: x => x.mvp_snapshot_id,
                        principalTable: "mvp_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_verification_checklists_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verification_checklist_items",
                columns: table => new
                {
                    checklist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    applicable = table.Column<bool>(type: "boolean", nullable: false),
                    not_applicable_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    @checked = table.Column<bool>(name: "checked", type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    checked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_checklist_items", x => new { x.checklist_id, x.item_key });
                    table.ForeignKey(
                        name: "FK_verification_checklist_items_verification_checklists_checkl~",
                        column: x => x.checklist_id,
                        principalTable: "verification_checklists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_verification_checklists_mvp_snapshot_id",
                table: "verification_checklists",
                column: "mvp_snapshot_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_verification_checklists_workspace_id_snapshot_version",
                table: "verification_checklists",
                columns: new[] { "workspace_id", "snapshot_version" },
                unique: true);

            // Snapshots locked before VERIFY existed get an empty list too: a locked definition can't be locked again.
            migrationBuilder.Sql("""
                INSERT INTO verification_checklists
                    (id, workspace_id, mvp_snapshot_id, snapshot_version, all_items_checked, completed_at, created_at, updated_at, revision)
                SELECT md5(s.id::text || clock_timestamp()::text)::uuid, s.workspace_id, s.id, s.version, false, NULL, now(), NULL, 0
                FROM mvp_snapshots s
                WHERE NOT EXISTS (SELECT 1 FROM verification_checklists c WHERE c.mvp_snapshot_id = s.id);

                INSERT INTO verification_checklist_items (checklist_id, item_key, applicable, checked)
                SELECT c.id, k.item_key, true, false
                FROM verification_checklists c
                CROSS JOIN (VALUES ('main_user_journey'), ('authentication'), ('production_config'),
                                   ('basic_crud'), ('error_handling')) AS k(item_key)
                WHERE NOT EXISTS (SELECT 1 FROM verification_checklist_items i WHERE i.checklist_id = c.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "verification_checklist_items");

            migrationBuilder.DropTable(
                name: "verification_checklists");
        }
    }
}
