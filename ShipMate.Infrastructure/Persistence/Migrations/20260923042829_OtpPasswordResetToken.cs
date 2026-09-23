using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OtpPasswordResetToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens");

            migrationBuilder.DropIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id_token_hash",
                table: "password_reset_tokens",
                columns: new[] { "user_id", "token_hash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_password_reset_tokens_user_id_token_hash",
                table: "password_reset_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");
        }
    }
}
