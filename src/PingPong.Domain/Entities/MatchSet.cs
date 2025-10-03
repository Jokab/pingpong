using System;

namespace PingPong.Domain.Entities;

public sealed class MatchSet
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public Match? Match { get; set; }

    public int SetNumber { get; set; }

    public int PlayerOneScore { get; set; }

    public int PlayerTwoScore { get; set; }
}
