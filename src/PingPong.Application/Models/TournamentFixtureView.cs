using PingPong.Domain.Entities;

namespace PingPong.Application.Models;

public sealed record TournamentFixtureView(
    Guid FixtureId,
    Guid TournamentId,
    Guid PlayerOneId,
    string PlayerOneName,
    Guid PlayerTwoId,
    string PlayerTwoName,
    TournamentFixtureStatus Status,
    Guid? WinnerPlayerId,
    Guid? MatchEventId,
    int RoundNumber,
    int Sequence);

