using PingPong.Domain.Entities;

namespace PingPong.Application.Models;

public sealed record TournamentSummary(
    Guid Id,
    string Name,
    string? Description,
    TournamentStatus Status,
    int DurationDays,
    int ParticipantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndsAt);

