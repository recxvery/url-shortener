namespace UrlShortener.Application.Abstractions;

public interface IClickRepository
{
    Task<long> CountSinceAsync(Guid linkId, DateTimeOffset since, CancellationToken cancellationToken);
}
