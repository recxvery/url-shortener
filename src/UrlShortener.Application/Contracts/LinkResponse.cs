namespace UrlShortener.Application.Contracts;

public sealed record LinkResponse(
    Guid Id,
    string Code,
    string ShortUrl,
    string OriginalUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    long ClickCount);
