using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "governance_suspended_at",
                table: "workspaces",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "governance_suspended_reason",
                table: "workspaces",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "build_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staging_branch = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    head_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    snapshot_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_build_submissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_build_submissions_users_submitted_by_user_id",
                        column: x => x.submitted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_build_submissions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_build_submissions_submitted_by_user_id",
                table: "build_submissions",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_build_submissions_workspace_id_feature_id",
                table: "build_submissions",
                columns: new[] { "workspace_id", "feature_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "build_submissions");

            migrationBuilder.DropColumn(
                name: "governance_suspended_at",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "governance_suspended_reason",
                table: "workspaces");
        }
    }
}
