using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Options;
using UrlShortener.Infrastructure.Caching;
using UrlShortener.Infrastructure.Messaging;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Infrastructure.Persistence.Repositories;
using UrlShortener.Infrastructure.Services;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgresConnection = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                postgresConnection,
                npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null)));

        services.Configure<ShortLinkOptions>(configuration.GetSection(ShortLinkOptions.SectionName));
        services.Configure<ClickIngestionOptions>(configuration.GetSection(ClickIngestionOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IShortCodeGenerator, Base62ShortCodeGenerator>();
        services.AddScoped<ILinkRepository, LinkRepository>();
        services.AddScoped<IClickRepository, ClickRepository>();

        services.AddStackExchangeRedisCache(options =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnection);
            redisOptions.AbortOnConnectFail = false;

            options.ConfigurationOptions = redisOptions;
            options.InstanceName = "urlshortener:";
        });

        services.AddSingleton<ILinkCache, RedisLinkCache>();
        services.AddSingleton<IClickQueue, ClickQueue>();
        services.AddHostedService<ClickFlushWorker>();

        return services;
    }
}
