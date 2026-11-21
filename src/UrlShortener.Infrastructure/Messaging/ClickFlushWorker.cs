using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using UrlShortener.Application.Abstractions;
using UrlShortener.Domain.Entities;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure.Messaging;

public sealed class ClickFlushWorker : BackgroundService
{
    private readonly IClickQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClickIngestionOptions _options;
    private readonly ILogger<ClickFlushWorker> _logger;

    public ClickFlushWorker(
        IClickQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<ClickIngestionOptions> options,
        ILogger<ClickFlushWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<ClickRecord>(_options.BatchSize);

        _logger.LogInformation(
            "Click flush worker started, batch size {BatchSize}, flush interval {FlushInterval}",
            _options.BatchSize,
            _options.FlushInterval);

        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                Drain(batch);

                if (batch.Count < _options.BatchSize)
                {
                    await Task.Delay(_options.FlushInterval, stoppingToken);
                    Drain(batch);
                }

                if (batch.Count > 0)
                {
                    await FlushSafelyAsync(batch, stoppingToken);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            Drain(batch);

            if (batch.Count > 0)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await FlushSafelyAsync(batch, timeout.Token);
            }
        }
    }

    private void Drain(List<ClickRecord> batch)
    {
        while (batch.Count < _options.BatchSize && _queue.Reader.TryRead(out var click))
        {
            batch.Add(click);
        }
    }

    private async Task FlushSafelyAsync(List<ClickRecord> batch, CancellationToken cancellationToken)
    {
        try
        {
            await FlushAsync(batch, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Click flush was cancelled, {ClickCount} clicks were not persisted", batch.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to flush {ClickCount} clicks", batch.Count);
        }
    }

    private async Task FlushAsync(List<ClickRecord> batch, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var aggregates = batch
            .GroupBy(x => x.LinkId)
            .Select(group => new ClickAggregate(
                group.Key,
                group.LongCount(),
                group.Max(x => x.OccurredAt)))
            .ToArray();

        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            foreach (var click in batch)
            {
                db.ClickEvents.Add(ClickEvent.Create(
                    click.LinkId,
                    click.OccurredAt,
                    click.IpAddress,
                    click.UserAgent,
                    click.Referrer));
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await UpdateCountersAsync(db, aggregates, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        _logger.LogDebug(
            "Flushed {ClickCount} clicks across {LinkCount} links",
            batch.Count,
            aggregates.Length);
    }

    private static async Task UpdateCountersAsync(
        AppDbContext db,
        ClickAggregate[] aggregates,
        CancellationToken cancellationToken)
    {
        if (aggregates.Length == 0)
        {
            return;
        }

        const string sql = """
            UPDATE short_links AS l
            SET click_count = l.click_count + b.clicks,
                last_accessed_at = GREATEST(l.last_accessed_at, b.last_click)
            FROM (
                SELECT *
                FROM unnest(@link_ids, @counts, @last_clicks) AS t(link_id, clicks, last_click)
            ) AS b
            WHERE l.id = b.link_id
            """;

        var parameters = new object[]
        {
            new NpgsqlParameter("link_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            {
                Value = aggregates.Select(x => x.LinkId).ToArray()
            },
            new NpgsqlParameter("counts", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
            {
                Value = aggregates.Select(x => x.Clicks).ToArray()
            },
            new NpgsqlParameter("last_clicks", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
            {
                Value = aggregates.Select(x => x.LastClick).ToArray()
            }
        };

        await db.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }

    private sealed record ClickAggregate(Guid LinkId, long Clicks, DateTimeOffset LastClick);
}
