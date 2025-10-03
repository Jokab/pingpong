namespace PingPong.Domain.Entities;

public sealed class MatchEventSet
{
    public Guid Id { get; set; }

    public Guid MatchEventId { get; set; }

    public MatchEvent? MatchEvent { get; set; }

    public int SetNumber { get; set; }

    public int PlayerOneScore { get; set; }

    public int PlayerTwoScore { get; set; }
}
