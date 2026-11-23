using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "complexity_budget_per_developer",
                table: "workspaces",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    complexity_budget_per_developer = table.Column<int>(type: "integer", nullable: false),
                    default_drift_threshold = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    maintenance_commit_patterns = table.Column<List<string>>(type: "text[]", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_settings", x => x.id);
                    table.CheckConstraint("ck_platform_settings_single_row", "id = 1");
                });

            migrationBuilder.InsertData(
                table: "platform_settings",
                columns: new[] { "id", "complexity_budget_per_developer", "default_drift_threshold", "maintenance_commit_patterns", "revision", "updated_at", "updated_by_user_id" },
                values: new object[] { 1, 10, 0.75m, new List<string> { "fix typo", "refactor", "bump version", "format code" }, 0, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropColumn(
                name: "complexity_budget_per_developer",
                table: "workspaces");
        }
    }
}
