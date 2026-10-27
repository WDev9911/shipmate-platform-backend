using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubAppInstallation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "github_installation_id",
                table: "workspaces",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "github_repo_is_organization",
                table: "workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "github_installation_id",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "github_repo_is_organization",
                table: "workspaces");
        }
    }
}
