using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LockFixesAndPersonaCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "persona_check_feature_id",
                table: "feature_challenges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "persona_conflict",
                table: "feature_challenges",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "persona_conflict_reason",
                table: "feature_challenges",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "branch_created_at",
                table: "feature_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "released_at",
                table: "feature_assignments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "persona_check_feature_id",
                table: "feature_challenges");

            migrationBuilder.DropColumn(
                name: "persona_conflict",
                table: "feature_challenges");

            migrationBuilder.DropColumn(
                name: "persona_conflict_reason",
                table: "feature_challenges");

            migrationBuilder.DropColumn(
                name: "branch_created_at",
                table: "feature_assignments");

            migrationBuilder.DropColumn(
                name: "released_at",
                table: "feature_assignments");
        }
    }
}
