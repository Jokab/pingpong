using PingPong.Domain.Tournaments;
namespace PingPong.Application.Tournaments;

public sealed record TournamentSummary(
    Guid Id,
    string Name,
    string? Description,
    TournamentStatus Status,
    int DurationDays,
    int ParticipantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndsAt
);
