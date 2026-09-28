using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShipMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DefineProductDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false, defaultValue: "draft"),
                    problem = table.Column<string>(type: "text", nullable: true),
                    solution = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_persona = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_definitions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_analysis_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vision_prompt_snapshot = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    raw_response = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_analysis_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_analysis_runs_product_definitions_product_definition_id",
                        column: x => x.product_definition_id,
                        principalTable: "product_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_analysis_runs_users_run_by_user_id",
                        column: x => x.run_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    origin = table.Column<string>(type: "character varying(23)", maxLength: 23, nullable: false),
                    sources = table.Column<string[]>(type: "text[]", nullable: false),
                    ai_verdict = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    ai_reason = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    possible_duplicate = table.Column<bool>(type: "boolean", nullable: false),
                    duplicate_of_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duplicate_reason = table.Column<string>(type: "text", nullable: true),
                    persona_conflict = table.Column<bool>(type: "boolean", nullable: false),
                    persona_conflict_reason = table.Column<string>(type: "text", nullable: true),
                    dev_decided = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_features_features_duplicate_of_id",
                        column: x => x.duplicate_of_id,
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_features_product_definitions_product_definition_id",
                        column: x => x.product_definition_id,
                        principalTable: "product_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_change_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    old_content = table.Column<string>(type: "jsonb", nullable: true),
                    new_content = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    customer_notified_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_change_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_change_logs_features_feature_id",
                        column: x => x.feature_id,
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feature_change_logs_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "feature_dependencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depends_on_feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_dependencies", x => x.id);
                    table.ForeignKey(
                        name: "FK_feature_dependencies_features_depends_on_feature_id",
                        column: x => x.depends_on_feature_id,
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_feature_dependencies_features_feature_id",
                        column: x => x.feature_id,
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_analysis_runs_product_definition_id",
                table: "ai_analysis_runs",
                column: "product_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_analysis_runs_run_by_user_id",
                table: "ai_analysis_runs",
                column: "run_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_change_logs_feature_id",
                table: "feature_change_logs",
                column: "feature_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_change_logs_performed_by_user_id",
                table: "feature_change_logs",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_dependencies_depends_on_feature_id",
                table: "feature_dependencies",
                column: "depends_on_feature_id");

            migrationBuilder.CreateIndex(
                name: "IX_feature_dependencies_feature_id_depends_on_feature_id",
                table: "feature_dependencies",
                columns: new[] { "feature_id", "depends_on_feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_features_duplicate_of_id",
                table: "features",
                column: "duplicate_of_id");

            migrationBuilder.CreateIndex(
                name: "IX_features_product_definition_id",
                table: "features",
                column: "product_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_definitions_workspace_id",
                table: "product_definitions",
                column: "workspace_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_analysis_runs");

            migrationBuilder.DropTable(
                name: "feature_change_logs");

            migrationBuilder.DropTable(
                name: "feature_dependencies");

            migrationBuilder.DropTable(
                name: "features");

            migrationBuilder.DropTable(
                name: "product_definitions");
        }
    }
}
