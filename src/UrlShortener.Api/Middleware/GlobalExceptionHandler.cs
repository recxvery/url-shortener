using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Exceptions;

namespace UrlShortener.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        switch (exception)
        {
            case ValidationException validationException:
                await Results.ValidationProblem(
                        new Dictionary<string, string[]>(validationException.Errors),
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "One or more validation errors occurred.")
                    .ExecuteAsync(httpContext);
                return true;

            case NotFoundException notFoundException:
                await Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Not found",
                        detail: notFoundException.Message)
                    .ExecuteAsync(httpContext);
                return true;

            case ConflictException conflictException:
                await Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Conflict",
                        detail: conflictException.Message)
                    .ExecuteAsync(httpContext);
                return true;

            default:
                _logger.LogError(
                    exception,
                    "Unhandled exception for {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path);

                await Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Internal server error")
                    .ExecuteAsync(httpContext);
                return true;
        }
    }
}
