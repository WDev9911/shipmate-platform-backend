using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "build_submissions",
                type: "character varying(19)",
                maxLength: 19,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(9)",
                oldMaxLength: 9);

            migrationBuilder.AddColumn<string>(
                name: "ai_model",
                table: "build_submissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ai_raw_response",
                table: "build_submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "analyzed_at",
                table: "build_submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "carried_from_submission_id",
                table: "build_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "confidence_score",
                table: "build_submissions",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failure_code",
                table: "build_submissions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "findings",
                table: "build_submissions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suggestions",
                table: "build_submissions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "suspected_feature_ids",
                table: "build_submissions",
                type: "uuid[]",
                nullable: false);

            migrationBuilder.AddColumn<decimal>(
                name: "threshold_at_check",
                table: "build_submissions",
                type: "numeric(4,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_model",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "ai_raw_response",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "analyzed_at",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "carried_from_submission_id",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "confidence_score",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "failure_code",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "findings",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "suggestions",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "suspected_feature_ids",
                table: "build_submissions");

            migrationBuilder.DropColumn(
                name: "threshold_at_check",
                table: "build_submissions");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "build_submissions",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(19)",
                oldMaxLength: 19);
        }
    }
}
