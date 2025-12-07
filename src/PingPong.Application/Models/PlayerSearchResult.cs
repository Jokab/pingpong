namespace PingPong.Application.Models;

public sealed record PlayerSearchResult(
    Guid PlayerId,
    string DisplayName,
    double Confidence,
    bool IsExactMatch
);
