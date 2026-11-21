namespace UrlShortener.Domain.Entities;

public sealed class ClickEvent
{
    public long Id { get; private set; }
    public Guid LinkId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Referrer { get; private set; }

    private ClickEvent()
    {
    }

    public static ClickEvent Create(
        Guid linkId,
        DateTimeOffset occurredAt,
        string? ipAddress,
        string? userAgent,
        string? referrer)
    {
        return new ClickEvent
        {
            LinkId = linkId,
            OccurredAt = occurredAt,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Referrer = referrer
        };
    }
}
