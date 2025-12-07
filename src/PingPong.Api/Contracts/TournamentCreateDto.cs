namespace PingPong.Api.Contracts;

public sealed record TournamentCreateDto(string Name, string? Description, int DurationDays);
