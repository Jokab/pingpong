using System.Text.Json.Serialization;

namespace PingPong.Api.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ScoredMatchSubmissionDto), typeDiscriminator: "scored")]
[JsonDerivedType(typeof(OutcomeOnlyMatchSubmissionDto), typeDiscriminator: "outcome")]
public abstract record MatchSubmissionDto(
    string PlayerOneName,
    string PlayerTwoName,
    DateOnly? MatchDate,
    string? SubmittedBy,
    Guid? TournamentFixtureId);

public sealed record ScoredMatchSubmissionDto(
    string PlayerOneName,
    string PlayerTwoName,
    DateOnly? MatchDate,
    IReadOnlyList<SetScoreDto> Sets,
    string? SubmittedBy,
    Guid? TournamentFixtureId) : MatchSubmissionDto(PlayerOneName, PlayerTwoName, MatchDate, SubmittedBy, TournamentFixtureId);

public sealed record OutcomeOnlyMatchSubmissionDto(
    string PlayerOneName,
    string PlayerTwoName,
    DateOnly? MatchDate,
    bool PlayerOneWon,
    IReadOnlyList<SetWinnerDto>? Sets,
    string? SubmittedBy,
    Guid? TournamentFixtureId) : MatchSubmissionDto(PlayerOneName, PlayerTwoName, MatchDate, SubmittedBy, TournamentFixtureId);

public sealed record SetScoreDto(int PlayerOneScore, int PlayerTwoScore);

public sealed record SetWinnerDto(int SetNumber, bool PlayerOneWon);
