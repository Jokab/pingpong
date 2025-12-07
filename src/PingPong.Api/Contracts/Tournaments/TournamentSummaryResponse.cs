using PingPong.Domain.Tournaments;
namespace PingPong.Api.Contracts.Tournaments;

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
