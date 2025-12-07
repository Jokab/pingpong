using PingPong.Domain.Players;
namespace PingPong.Domain.Tournaments;

public sealed class TournamentParticipant
{
    public Guid Id { get; set; }

    public Guid TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    public Guid PlayerId { get; set; }

    public Player? Player { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public int MatchesPlayed { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Points { get; set; }
}
