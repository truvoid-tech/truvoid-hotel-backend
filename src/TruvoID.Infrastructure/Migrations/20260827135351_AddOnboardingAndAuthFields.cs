using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruvoID.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingAndAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_reset_token",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "password_reset_token_expiry",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cac_certificate_url",
                table: "institutions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cac_rc_number",
                table: "institutions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "compliance_accepted",
                table: "institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "compliance_accepted_at",
                table: "institutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "data_processing_agreed",
                table: "institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "expected_monthly_volume",
                table: "institutions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "onboarding_completed",
                table: "institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "onboarding_step",
                table: "institutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "primary_use_case",
                table: "institutions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "reseller_acknowledged",
                table: "institutions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_is_revoked",
                table: "refresh_tokens",
                columns: new[] { "user_id", "is_revoked" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "last_login_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_token",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_token_expiry",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "cac_certificate_url",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "cac_rc_number",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "compliance_accepted",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "compliance_accepted_at",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "data_processing_agreed",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "expected_monthly_volume",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "onboarding_completed",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "onboarding_step",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "primary_use_case",
                table: "institutions");

            migrationBuilder.DropColumn(
                name: "reseller_acknowledged",
                table: "institutions");
        }
    }
}
