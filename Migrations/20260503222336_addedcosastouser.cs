using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AixecAPI.Migrations
{
    /// <inheritdoc />
    public partial class addedcosastouser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Money",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlayedMatches",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WonMatches",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Money",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlayedMatches",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WonMatches",
                table: "Users");
        }
    }
}
