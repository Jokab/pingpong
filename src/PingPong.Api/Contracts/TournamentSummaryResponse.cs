using PingPong.Domain.Entities;

namespace PingPong.Api.Contracts;

public sealed record TournamentSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    TournamentStatus StatusValue,
    int DurationDays,
    int ParticipantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndsAt
);
