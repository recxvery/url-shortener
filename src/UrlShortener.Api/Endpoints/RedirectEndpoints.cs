using UrlShortener.Application.Abstractions;
using UrlShortener.Application.Services;

namespace UrlShortener.Api.Endpoints;

public static class RedirectEndpoints
{
    public static IEndpointRouteBuilder MapRedirectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{code:length(4,16)}", RedirectAsync)
            .WithName("RedirectToOriginalUrl")
            .WithTags("Redirect")
            .WithSummary("Redirect to the original URL")
            .WithDescription("Looks the code up in Redis first, falls back to PostgreSQL and queues a click event.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RedirectAsync(
        string code,
        LinkService links,
        IClickQueue clickQueue,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var link = await links.ResolveAsync(code, cancellationToken);
        if (link is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not found",
                detail: $"Short link '{code}' was not found or has expired.");
        }

        clickQueue.TryEnqueue(ClickRecord.Create(
            link.Id,
            DateTimeOffset.UtcNow,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            httpContext.Request.Headers.Referer.ToString()));

        return TypedResults.Redirect(link.OriginalUrl, permanent: false);
    }
}
