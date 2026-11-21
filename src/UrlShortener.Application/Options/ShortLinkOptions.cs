namespace UrlShortener.Application.Options;

public sealed class ShortLinkOptions
{
    public const string SectionName = "ShortLinks";

    public int CodeLength { get; set; } = 7;
    public int MinCustomCodeLength { get; set; } = 4;
    public int MaxCustomCodeLength { get; set; } = 16;
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(1);
    public string? PublicBaseUrl { get; set; }
}
