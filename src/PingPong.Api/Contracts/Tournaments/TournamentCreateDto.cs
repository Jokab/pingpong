namespace PingPong.Api.Contracts.Tournaments;

public sealed record TournamentCreateDto(string Name, string? Description, int DurationDays);
