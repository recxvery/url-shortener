using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstractions;

namespace UrlShortener.Infrastructure.Persistence.Repositories;

public sealed class ClickRepository : IClickRepository
{
    private readonly AppDbContext _db;

    public ClickRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<long> CountSinceAsync(Guid linkId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        return _db.ClickEvents
            .AsNoTracking()
            .LongCountAsync(x => x.LinkId == linkId && x.OccurredAt >= since, cancellationToken);
    }
}
