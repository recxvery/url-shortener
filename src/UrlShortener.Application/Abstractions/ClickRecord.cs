namespace UrlShortener.Application.Abstractions;

public sealed record ClickRecord(
    Guid LinkId,
    DateTimeOffset OccurredAt,
    string? IpAddress,
    string? UserAgent,
    string? Referrer)
{
    public static ClickRecord Create(
        Guid linkId,
        DateTimeOffset occurredAt,
        string? ipAddress,
        string? userAgent,
        string? referrer)
    {
        return new ClickRecord(
            linkId,
            occurredAt,
            Clean(ipAddress, 45),
            Clean(userAgent, 512),
            Clean(referrer, 2048));
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
