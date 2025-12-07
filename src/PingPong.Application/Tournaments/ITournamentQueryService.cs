namespace PingPong.Application.Tournaments;

public interface ITournamentQueryService
{
    Task<IReadOnlyList<TournamentSummary>> GetTournamentsAsync(
        CancellationToken cancellationToken = default
    );

    Task<TournamentDetails?> GetTournamentAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<TournamentFixtureView>> GetFixturesAsync(
        Guid tournamentId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<OpenFixtureOption>> GetOpenFixturesAsync(
        string playerOneName,
        string playerTwoName,
        CancellationToken cancellationToken = default
    );
}
