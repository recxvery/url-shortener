namespace UrlShortener.Domain.Entities;

public sealed class ShortLink
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string OriginalUrl { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsActive { get; private set; }
    public long ClickCount { get; private set; }
    public DateTimeOffset? LastAccessedAt { get; private set; }

    private ShortLink()
    {
    }

    public static ShortLink Create(string code, string originalUrl, DateTimeOffset createdAt, DateTimeOffset? expiresAt)
    {
        return new ShortLink
        {
            Id = Guid.NewGuid(),
            Code = code,
            OriginalUrl = originalUrl,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            IsActive = true
        };
    }

    public bool CanRedirect(DateTimeOffset now)
    {
        return IsActive && !IsExpired(now);
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAt.HasValue && ExpiresAt.Value <= now;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
