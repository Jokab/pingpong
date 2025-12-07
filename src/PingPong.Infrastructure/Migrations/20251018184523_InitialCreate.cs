using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PingPong.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    NormalizedName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamptz",
                        precision: 3,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "PlayerAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AliasName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConfidenceScore = table.Column<double>(
                        type: "REAL",
                        precision: 5,
                        scale: 4,
                        nullable: false
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamptz",
                        precision: 3,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerAliases_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PlayerRatings",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentRating = table.Column<double>(
                        type: "REAL",
                        precision: 8,
                        scale: 2,
                        nullable: false
                    ),
                    LastUpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamptz",
                        precision: 3,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRatings", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_PlayerRatings_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    LatestEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamptz",
                        precision: 3,
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamptz",
                        precision: 3,
                        nullable: false
                    ),
                    PlayerOneSetsWon = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerTwoSetsWon = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Players_PlayerOneId",
                        column: x => x.PlayerOneId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_Matches_Players_PlayerTwoId",
                        column: x => x.PlayerTwoId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MatchEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    SupersedesEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlayerOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamptz",
                        precision: 3,
                        nullable: false
                    ),
                    SubmittedBy = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchEvents_MatchEvents_SupersedesEventId",
                        column: x => x.SupersedesEventId,
                        principalTable: "MatchEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MatchEvents_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id"
                    );
                    table.ForeignKey(
                        name: "FK_MatchEvents_Players_PlayerOneId",
                        column: x => x.PlayerOneId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_MatchEvents_Players_PlayerTwoId",
                        column: x => x.PlayerTwoId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MatchSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerOneScore = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerTwoScore = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchSets_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MatchEventSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerOneScore = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerTwoScore = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEventSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchEventSets_MatchEvents_MatchEventId",
                        column: x => x.MatchEventId,
                        principalTable: "MatchEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlayerOneId",
                table: "Matches",
                column: "PlayerOneId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PlayerTwoId",
                table: "Matches",
                column: "PlayerTwoId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Matches_PrimaryEventId",
                table: "Matches",
                column: "PrimaryEventId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId",
                table: "MatchEvents",
                column: "MatchId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_PlayerOneId",
                table: "MatchEvents",
                column: "PlayerOneId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_PlayerTwoId",
                table: "MatchEvents",
                column: "PlayerTwoId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_SupersedesEventId",
                table: "MatchEvents",
                column: "SupersedesEventId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_MatchEventSets_MatchEventId_SetNumber",
                table: "MatchEventSets",
                columns: new[] { "MatchEventId", "SetNumber" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MatchSets_MatchId_SetNumber",
                table: "MatchSets",
                columns: new[] { "MatchId", "SetNumber" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAliases_PlayerId_AliasName",
                table: "PlayerAliases",
                columns: new[] { "PlayerId", "AliasName" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Players_NormalizedName",
                table: "Players",
                column: "NormalizedName",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_MatchEvents_PrimaryEventId",
                table: "Matches",
                column: "PrimaryEventId",
                principalTable: "MatchEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_MatchEvents_PrimaryEventId",
                table: "Matches"
            );

            migrationBuilder.DropTable(name: "MatchEventSets");

            migrationBuilder.DropTable(name: "MatchSets");

            migrationBuilder.DropTable(name: "PlayerAliases");

            migrationBuilder.DropTable(name: "PlayerRatings");

            migrationBuilder.DropTable(name: "MatchEvents");

            migrationBuilder.DropTable(name: "Matches");

            migrationBuilder.DropTable(name: "Players");
        }
    }
}
