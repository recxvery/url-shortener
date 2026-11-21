namespace UrlShortener.Application.Contracts;

public sealed record CreateLinkRequest(
    string OriginalUrl,
    string? CustomCode = null,
    DateTimeOffset? ExpiresAt = null);
