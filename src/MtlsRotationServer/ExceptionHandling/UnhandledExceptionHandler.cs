using Microsoft.AspNetCore.Diagnostics;

namespace MtlsRotationServer.ExceptionHandling;

public sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception.");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "server_fault",
                message = "An unexpected error occurred."
            },
            cancellationToken);

        return true;
    }
}