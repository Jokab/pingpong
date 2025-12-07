using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PingPong.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchEventDiscriminator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add EventKind column as nullable first
            migrationBuilder.AddColumn<string>(
                name: "EventKind",
                table: "MatchEvents",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "PlayerOneWon",
                table: "MatchEvents",
                type: "boolean",
                nullable: true
            );

            // Set discriminator values for existing rows
            // Events with sets should be "Scored"
            migrationBuilder.Sql(
                @"
                UPDATE ""MatchEvents""
                SET ""EventKind"" = 'Scored'
                WHERE ""EventKind"" IS NULL
                  AND EXISTS (
                      SELECT 1 FROM ""MatchEventSets"" 
                      WHERE ""MatchEventSets"".""MatchEventId"" = ""MatchEvents"".""Id""
                  );
            "
            );

            // Events without sets but with PlayerOneWon should be "Outcome"
            // (This handles future outcome-only events, but for now all existing events have sets)
            migrationBuilder.Sql(
                @"
                UPDATE ""MatchEvents""
                SET ""EventKind"" = 'Outcome'
                WHERE ""EventKind"" IS NULL
                  AND ""PlayerOneWon"" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM ""MatchEventSets"" 
                      WHERE ""MatchEventSets"".""MatchEventId"" = ""MatchEvents"".""Id""
                  );
            "
            );

            // Default any remaining NULL values to "Scored" (for existing events)
            migrationBuilder.Sql(
                @"
                UPDATE ""MatchEvents""
                SET ""EventKind"" = 'Scored'
                WHERE ""EventKind"" IS NULL;
            "
            );

            // Now make the column non-nullable
            migrationBuilder.AlterColumn<string>(
                name: "EventKind",
                table: "MatchEvents",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(13)",
                oldNullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EventKind", table: "MatchEvents");

            migrationBuilder.DropColumn(name: "PlayerOneWon", table: "MatchEvents");
        }
    }
}
