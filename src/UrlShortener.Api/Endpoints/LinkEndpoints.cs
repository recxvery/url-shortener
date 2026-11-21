using UrlShortener.Application.Contracts;
using UrlShortener.Application.Services;

namespace UrlShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/links")
            .WithTags("Links");

        group.MapPost("/", CreateAsync)
            .WithName("CreateLink")
            .WithSummary("Create a short link")
            .WithDescription("Creates a short link for an absolute http or https URL. A custom code and an expiration date are optional.")
            .Produces<LinkResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{code}", GetAsync)
            .WithName("GetLink")
            .WithSummary("Get link details")
            .Produces<LinkResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{code}/stats", GetStatsAsync)
            .WithName("GetLinkStats")
            .WithSummary("Get click statistics")
            .WithDescription("Returns the total click count and the number of clicks for the last 24 hours.")
            .Produces<LinkStatsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{code}", DeactivateAsync)
            .WithName("DeactivateLink")
            .WithSummary("Deactivate a short link")
            .WithDescription("Deactivates the link, removes it from the cache and keeps its click history.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateLinkRequest request,
        LinkService links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var link = await links.CreateAsync(request, GetBaseUrl(httpContext), cancellationToken);
        return TypedResults.Created($"/api/links/{link.Code}", link);
    }

    private static async Task<IResult> GetAsync(
        string code,
        LinkService links,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var link = await links.GetAsync(code, GetBaseUrl(httpContext), cancellationToken);
        return TypedResults.Ok(link);
    }

    private static async Task<IResult> GetStatsAsync(
        string code,
        LinkService links,
        CancellationToken cancellationToken)
    {
        var stats = await links.GetStatsAsync(code, cancellationToken);
        return TypedResults.Ok(stats);
    }

    private static async Task<IResult> DeactivateAsync(
        string code,
        LinkService links,
        CancellationToken cancellationToken)
    {
        await links.DeactivateAsync(code, cancellationToken);
        return TypedResults.NoContent();
    }

    private static string GetBaseUrl(HttpContext httpContext)
    {
        return $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";
    }
}
