using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLockFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "skill_level",
                table: "workspace_members",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "complexity",
                table: "features",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "proposed_complexity",
                table: "features",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "feature_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_assignments_users_assigned_user_id",
                        column: x => x.assigned_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_assignments_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    proposed_complexity = table.Column<int>(type: "integer", nullable: true),
                    complexity = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    action = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    removed_feature_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dependents_resolution = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    replacement_feature_id = table.Column<Guid>(type: "uuid", nullable: true),
                    committed_removal_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    resulting_snapshot_version = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_challenges", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_challenges_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mvp_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dev_count_at_lock = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mvp_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_mvp_snapshots_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_dependencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depends_on_feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_dependencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_snapshot_dependencies_mvp_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "mvp_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    origin = table.Column<string>(type: "character varying(23)", maxLength: 23, nullable: false),
                    complexity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_snapshot_features_mvp_snapshots_snapshot_id",
                        column: x => x.snapshot_id,
                        principalTable: "mvp_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feature_assignments_assigned_user_id",
                table: "feature_assignments",
                column: "assigned_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_assignments_feature_id",
                table: "feature_assignments",
                column: "feature_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feature_assignments_workspace_id",
                table: "feature_assignments",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_challenges_workspace_id",
                table: "feature_challenges",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_mvp_snapshots_workspace_id_version",
                table: "mvp_snapshots",
                columns: new[] { "workspace_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_dependencies_snapshot_id_feature_id_depends_on_fea~",
                table: "snapshot_dependencies",
                columns: new[] { "snapshot_id", "feature_id", "depends_on_feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_features_snapshot_id_feature_id",
                table: "snapshot_features",
                columns: new[] { "snapshot_id", "feature_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_assignments");

            migrationBuilder.DropTable(
                name: "feature_challenges");

            migrationBuilder.DropTable(
                name: "snapshot_dependencies");

            migrationBuilder.DropTable(
                name: "snapshot_features");

            migrationBuilder.DropTable(
                name: "mvp_snapshots");

            migrationBuilder.DropColumn(
                name: "skill_level",
                table: "workspace_members");

            migrationBuilder.DropColumn(
                name: "complexity",
                table: "features");

            migrationBuilder.DropColumn(
                name: "proposed_complexity",
                table: "features");
        }
    }
}
