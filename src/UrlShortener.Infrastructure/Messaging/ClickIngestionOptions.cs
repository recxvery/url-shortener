namespace UrlShortener.Infrastructure.Messaging;

public sealed class ClickIngestionOptions
{
    public const string SectionName = "ClickIngestion";

    public int QueueCapacity { get; set; } = 100_000;
    public int BatchSize { get; set; } = 500;
    public int FlushIntervalMs { get; set; } = 1_000;

    public TimeSpan FlushInterval => TimeSpan.FromMilliseconds(Math.Max(50, FlushIntervalMs));
}
