namespace PingPong.Domain.Entities;

public sealed class TournamentFixture
{
    public Guid Id { get; set; }

    public Guid TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    public Guid PlayerOneId { get; set; }

    public Player? PlayerOne { get; set; }

    public Guid PlayerTwoId { get; set; }

    public Player? PlayerTwo { get; set; }

    public TournamentFixtureStatus Status { get; set; } = TournamentFixtureStatus.Pending;

    public Guid? MatchEventId { get; set; }

    public MatchEvent? MatchEvent { get; set; }

    public Guid? WinnerPlayerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int RoundNumber { get; set; }

    public int Sequence { get; set; }
}

