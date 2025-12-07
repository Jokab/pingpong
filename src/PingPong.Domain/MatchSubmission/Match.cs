using PingPong.Domain.Players;
namespace PingPong.Domain.MatchSubmission;

public sealed class Match
{
    public Guid Id { get; set; }

    public Guid PlayerOneId { get; set; }

    public Player? PlayerOne { get; set; }

    public Guid PlayerTwoId { get; set; }

    public Player? PlayerTwo { get; set; }

    public DateOnly MatchDate { get; set; }

    public MatchStatus Status { get; set; }

    public Guid? PrimaryEventId { get; set; }

    public MatchEvent? PrimaryEvent { get; set; }

    public Guid? LatestEventId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int PlayerOneSetsWon { get; set; }

    public int PlayerTwoSetsWon { get; set; }

    public ICollection<MatchSet> Sets { get; set; } = new List<MatchSet>();

    public ICollection<MatchEvent> Events { get; set; } = new List<MatchEvent>();
}
