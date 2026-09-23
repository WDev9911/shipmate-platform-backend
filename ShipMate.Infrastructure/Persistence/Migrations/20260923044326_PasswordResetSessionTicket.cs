using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PasswordResetSessionTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "session_ticket_hash",
                table: "password_reset_tokens",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "verified_at",
                table: "password_reset_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_session_ticket_hash",
                table: "password_reset_tokens",
                column: "session_ticket_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_password_reset_tokens_session_ticket_hash",
                table: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "session_ticket_hash",
                table: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "verified_at",
                table: "password_reset_tokens");
        }
    }
}
