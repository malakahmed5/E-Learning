using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearningHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountInviteAndTwoFactorStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "two_factor_secret",
                table: "accounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invite_code_hash",
                table: "accounts",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "invite_expires_at",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "two_factor_last_step",
                table: "accounts",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invite_code_hash",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "invite_expires_at",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "two_factor_last_step",
                table: "accounts");

            migrationBuilder.AlterColumn<string>(
                name: "two_factor_secret",
                table: "accounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
