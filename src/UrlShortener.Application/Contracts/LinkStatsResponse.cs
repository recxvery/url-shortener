namespace UrlShortener.Application.Contracts;

public sealed record LinkStatsResponse(
    string Code,
    string OriginalUrl,
    long TotalClicks,
    long ClicksLast24Hours,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastAccessedAt,
    bool IsActive);
