using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.Infrastructure.Messaging;

public sealed class ClickQueue : IClickQueue
{
    private readonly Channel<ClickRecord> _channel;
    private readonly ILogger<ClickQueue> _logger;
    private long _droppedCount;

    public ClickQueue(IOptions<ClickIngestionOptions> options, ILogger<ClickQueue> logger)
    {
        _logger = logger;

        _channel = Channel.CreateBounded<ClickRecord>(new BoundedChannelOptions(Math.Max(1, options.Value.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelReader<ClickRecord> Reader => _channel.Reader;

    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public bool TryEnqueue(ClickRecord click)
    {
        if (_channel.Writer.TryWrite(click))
        {
            return true;
        }

        var dropped = Interlocked.Increment(ref _droppedCount);
        if (dropped == 1 || dropped % 1_000 == 0)
        {
            _logger.LogWarning("Click queue is full, {DroppedCount} clicks have been dropped", dropped);
        }

        return false;
    }
}
