using PingPong.Application.Models;

namespace PingPong.Application.Interfaces;

public interface IMatchSubmissionService
{
    Task<MatchSubmissionResult> SubmitMatchAsync(MatchSubmissionRequest request, CancellationToken cancellationToken = default);
}
