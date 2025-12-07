using PingPong.Domain.Entities;

namespace PingPong.Api.Contracts;

public sealed record TournamentFixtureResponse(
    Guid Id,
    Guid TournamentId,
    Guid PlayerOneId,
    string PlayerOneName,
    Guid PlayerTwoId,
    string PlayerTwoName,
    TournamentFixtureStatus StatusValue,
    Guid? WinnerPlayerId,
    Guid? MatchEventId,
    int RoundNumber,
    int Sequence
)
{
    public string GetWinnerName()
    {
        if (WinnerPlayerId is null)
        {
            return string.Empty;
        }

        return WinnerPlayerId == PlayerOneId ? PlayerOneName : PlayerTwoName;
    }
}
