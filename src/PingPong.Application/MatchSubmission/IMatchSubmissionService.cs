using PingPong.Application.MatchSubmission;

namespace PingPong.Application.MatchSubmission;

public interface IMatchSubmissionService
{
    Task<MatchSubmissionResult> SubmitMatchAsync(
        MatchSubmissionRequest request,
        CancellationToken cancellationToken = default
    );
}
