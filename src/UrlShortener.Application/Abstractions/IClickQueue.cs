using System.Threading.Channels;

namespace UrlShortener.Application.Abstractions;

public interface IClickQueue
{
    ChannelReader<ClickRecord> Reader { get; }
    bool TryEnqueue(ClickRecord click);
}
