using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Contracts;
using UrlShortener.Application.Exceptions;
using UrlShortener.Application.Options;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Application.Services;

public sealed class LinkService
{
    private const int MaxCodeAllocationAttempts = 5;
    private static readonly Regex CustomCodePattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    private readonly ILinkRepository _links;
    private readonly IClickRepository _clicks;
    private readonly ILinkCache _cache;
    private readonly IShortCodeGenerator _codeGenerator;
    private readonly ShortLinkOptions _options;
    private readonly TimeProvider _timeProvider;

    public LinkService(
        ILinkRepository links,
        IClickRepository clicks,
        ILinkCache cache,
        IShortCodeGenerator codeGenerator,
        IOptions<ShortLinkOptions> options,
        TimeProvider timeProvider)
    {
        _links = links;
        _clicks = clicks;
        _cache = cache;
        _codeGenerator = codeGenerator;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<LinkResponse> CreateAsync(
        CreateLinkRequest request,
        string requestBaseUrl,
        CancellationToken cancellationToken)
    {
        var customCode = NormalizeCustomCode(request);
        Validate(request, customCode);

        for (var attempt = 0; attempt < MaxCodeAllocationAttempts; attempt++)
        {
            var code = customCode ?? _codeGenerator.Generate();
            var link = ShortLink.Create(
                code,
                request.OriginalUrl.Trim(),
                _timeProvider.GetUtcNow(),
                request.ExpiresAt);

            if (await _links.TryAddAsync(link, cancellationToken))
            {
                return Map(link, ResolveBaseUrl(requestBaseUrl));
            }

            if (customCode is not null)
            {
                throw new ConflictException($"Short code '{customCode}' is already in use.");
            }
        }

        throw new ConflictException("Failed to allocate a unique short code. Please retry.");
    }

    public async Task<CachedLink?> ResolveAsync(string code, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetAsync(code, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var link = await _links.GetByCodeAsync(code, cancellationToken);
        if (link is null || !link.CanRedirect(_timeProvider.GetUtcNow()))
        {
            return null;
        }

        var cachedLink = new CachedLink(link.Id, link.OriginalUrl);
        var ttl = _options.CacheTtl;

        if (link.ExpiresAt is { } expiresAt)
        {
            var remaining = expiresAt - _timeProvider.GetUtcNow();
            if (remaining < ttl)
            {
                ttl = remaining;
            }
        }

        if (ttl > TimeSpan.Zero)
        {
            await _cache.SetAsync(code, cachedLink, ttl, cancellationToken);
        }

        return cachedLink;
    }

    public async Task<LinkResponse> GetAsync(string code, string requestBaseUrl, CancellationToken cancellationToken)
    {
        var link = await _links.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException($"Short link '{code}' was not found.");

        return Map(link, ResolveBaseUrl(requestBaseUrl));
    }

    public async Task<LinkStatsResponse> GetStatsAsync(string code, CancellationToken cancellationToken)
    {
        var link = await _links.GetByCodeAsync(code, cancellationToken)
            ?? throw new NotFoundException($"Short link '{code}' was not found.");

        var since = _timeProvider.GetUtcNow().AddHours(-24);
        var recentClicks = await _clicks.CountSinceAsync(link.Id, since, cancellationToken);

        return new LinkStatsResponse(
            link.Code,
            link.OriginalUrl,
            link.ClickCount,
            recentClicks,
            link.CreatedAt,
            link.ExpiresAt,
            link.LastAccessedAt,
            link.IsActive);
    }

    public async Task DeactivateAsync(string code, CancellationToken cancellationToken)
    {
        var deactivated = await _links.DeactivateAsync(code, cancellationToken);
        if (!deactivated)
        {
            throw new NotFoundException($"Short link '{code}' was not found.");
        }

        await _cache.RemoveAsync(code, cancellationToken);
    }

    private void Validate(CreateLinkRequest request, string? customCode)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.OriginalUrl)
            || !Uri.TryCreate(request.OriginalUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors["originalUrl"] = ["A valid absolute URL with http or https scheme is required."];
        }
        else if (request.OriginalUrl.Trim().Length > 2048)
        {
            errors["originalUrl"] = ["URL must not exceed 2048 characters."];
        }

        if (customCode is not null)
        {
            if (customCode.Length < _options.MinCustomCodeLength || customCode.Length > _options.MaxCustomCodeLength)
            {
                errors["customCode"] =
                [
                    $"Custom code must be between {_options.MinCustomCodeLength} and {_options.MaxCustomCodeLength} characters long."
                ];
            }
            else if (!CustomCodePattern.IsMatch(customCode))
            {
                errors["customCode"] = ["Custom code may contain only letters, digits, underscores and hyphens."];
            }
        }

        if (request.ExpiresAt is { } expiresAt && expiresAt <= _timeProvider.GetUtcNow())
        {
            errors["expiresAt"] = ["Expiration time must be in the future."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static string? NormalizeCustomCode(CreateLinkRequest request)
    {
        return string.IsNullOrWhiteSpace(request.CustomCode) ? null : request.CustomCode.Trim();
    }

    private string ResolveBaseUrl(string requestBaseUrl)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? requestBaseUrl
            : _options.PublicBaseUrl;

        return baseUrl.TrimEnd('/');
    }

    private static LinkResponse Map(ShortLink link, string baseUrl)
    {
        return new LinkResponse(
            link.Id,
            link.Code,
            $"{baseUrl}/{link.Code}",
            link.OriginalUrl,
            link.CreatedAt,
            link.ExpiresAt,
            link.IsActive,
            link.ClickCount);
    }
}
