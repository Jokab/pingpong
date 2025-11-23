using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PingPong.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchEventSetWinner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PlayerTwoScore",
                table: "MatchEventSets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerOneScore",
                table: "MatchEventSets",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "PlayerOneWon",
                table: "MatchEventSets",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerOneWon",
                table: "MatchEventSets");

            migrationBuilder.AlterColumn<int>(
                name: "PlayerTwoScore",
                table: "MatchEventSets",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PlayerOneScore",
                table: "MatchEventSets",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
