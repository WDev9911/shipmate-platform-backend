using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OtpEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_token_hash",
                table: "email_verification_tokens");

            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_user_id",
                table: "email_verification_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_user_id_token_hash",
                table: "email_verification_tokens",
                columns: new[] { "user_id", "token_hash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_user_id_token_hash",
                table: "email_verification_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_token_hash",
                table: "email_verification_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_user_id",
                table: "email_verification_tokens",
                column: "user_id");
        }
    }
}
