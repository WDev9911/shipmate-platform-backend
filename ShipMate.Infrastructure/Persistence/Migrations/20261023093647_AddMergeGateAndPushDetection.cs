using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeGateAndPushDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "github_merge_gate_enabled",
                table: "workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "last_audited_main_sha",
                table: "workspaces",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "flagged_head_sha",
                table: "feature_assignments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "github_merge_gate_enabled",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "last_audited_main_sha",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "flagged_head_sha",
                table: "feature_assignments");
        }
    }
}
