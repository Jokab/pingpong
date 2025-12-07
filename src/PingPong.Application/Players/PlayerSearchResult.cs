namespace PingPong.Application.Players;

public sealed record PlayerSearchResult(
    Guid PlayerId,
    string DisplayName,
    double Confidence,
    bool IsExactMatch
);
