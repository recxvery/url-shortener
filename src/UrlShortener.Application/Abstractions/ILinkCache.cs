namespace UrlShortener.Application.Abstractions;

public sealed record CachedLink(Guid Id, string OriginalUrl);

public interface ILinkCache
{
    Task<CachedLink?> GetAsync(string code, CancellationToken cancellationToken);
    Task SetAsync(string code, CachedLink link, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string code, CancellationToken cancellationToken);
}
