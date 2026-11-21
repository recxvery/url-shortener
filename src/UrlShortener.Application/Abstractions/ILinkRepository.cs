using UrlShortener.Domain.Entities;

namespace UrlShortener.Application.Abstractions;

public interface ILinkRepository
{
    Task<ShortLink?> GetByCodeAsync(string code, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(string code, CancellationToken cancellationToken);
}
