namespace PingPong.Api.Contracts.MatchSubmission;

public sealed record MatchSubmissionResponse(Guid MatchId, Guid EventId);
