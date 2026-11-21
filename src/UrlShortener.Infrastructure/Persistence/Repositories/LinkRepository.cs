using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Persistence.Repositories;

public sealed class LinkRepository : ILinkRepository
{
    private readonly AppDbContext _db;

    public LinkRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ShortLink?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return _db.ShortLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
    }

    public async Task<bool> TryAddAsync(ShortLink link, CancellationToken cancellationToken)
    {
        _db.ShortLinks.Add(link);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _db.Entry(link).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> DeactivateAsync(string code, CancellationToken cancellationToken)
    {
        var affected = await _db.ShortLinks
            .Where(x => x.Code == code)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false), cancellationToken);

        return affected > 0;
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is Npgsql.PostgresException
        {
            SqlState: Npgsql.PostgresErrorCodes.UniqueViolation
        };
    }
}
