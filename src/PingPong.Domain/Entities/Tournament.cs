namespace PingPong.Domain.Entities;

public sealed class Tournament
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DurationDays { get; set; }

    public int PointsPerWin { get; set; } = 1;

    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<TournamentParticipant> Participants { get; set; } = new List<TournamentParticipant>();

    public ICollection<TournamentFixture> Fixtures { get; set; } = new List<TournamentFixture>();
}

