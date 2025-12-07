using PingPong.Application.Models;
using PingPong.Application.Tournaments;

namespace PingPong.Application.Tournaments;

public interface ITournamentCommandService
{
    Task<TournamentSummary> CreateTournamentAsync(
        CreateTournamentRequest request,
        CancellationToken cancellationToken = default
    );

    Task<TournamentSummary> StartTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default
    );

    Task<TournamentStandingRow> JoinTournamentAsync(
        Guid tournamentId,
        string playerName,
        CancellationToken cancellationToken = default
    );

    Task LeaveTournamentAsync(
        Guid tournamentId,
        string playerName,
        CancellationToken cancellationToken = default
    );

    Task RecordFixtureResultAsync(
        Guid fixtureId,
        Guid winnerPlayerId,
        Guid matchEventId,
        CancellationToken cancellationToken = default
    );
}
