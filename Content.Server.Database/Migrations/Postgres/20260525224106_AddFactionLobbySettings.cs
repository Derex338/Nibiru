using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddFactionLobbySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "faction_filter_gender",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "faction_filter_name",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "faction_filter_skincolors",
                table: "profile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "faction_filter_species",
                table: "profile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "faction_logo16",
                table: "profile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "faction_logo8",
                table: "profile",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "faction_logo_background",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "faction_filter_gender",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "faction_filter_name",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "faction_filter_skincolors",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "faction_filter_species",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "faction_logo16",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "faction_logo8",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "faction_logo_background",
                table: "profile");
        }
    }
}
