using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AixecAPI.Migrations
{
    /// <inheritdoc />
    public partial class changedIsPassiveToAbility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "isPassive",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "imageUrl",
                table: "Cards",
                newName: "ImageUrl");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Cards",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsPassive",
                table: "Ability",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPassive",
                table: "Ability");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Cards",
                newName: "imageUrl");

            migrationBuilder.UpdateData(
                table: "Cards",
                keyColumn: "imageUrl",
                keyValue: null,
                column: "imageUrl",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "imageUrl",
                table: "Cards",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "isPassive",
                table: "Cards",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
