namespace PingPong.Api.Contracts;

public sealed record MatchSubmissionDto(
    string PlayerOneName,
    string PlayerTwoName,
    DateOnly? MatchDate,
    IReadOnlyList<SetScoreDto>? Sets,
    string? SubmittedBy);

public sealed record SetScoreDto(int PlayerOneScore, int PlayerTwoScore);
