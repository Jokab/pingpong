using System;
using System.Collections.Generic;

namespace PingPong.Domain.Entities;

public sealed class MatchEvent
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public Match? Match { get; set; }

    public MatchEventType EventType { get; set; }

    public Guid? SupersedesEventId { get; set; }

    public MatchEvent? SupersededEvent { get; set; }

    public Guid PlayerOneId { get; set; }

    public Guid PlayerTwoId { get; set; }

    public Player? PlayerOne { get; set; }

    public Player? PlayerTwo { get; set; }

    public DateOnly MatchDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? SubmittedBy { get; set; }

    public ICollection<MatchEventSet> Sets { get; set; } = new List<MatchEventSet>();
}
