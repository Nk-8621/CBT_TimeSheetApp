using System.Net;
using Meridian.Application.Exceptions;

namespace Meridian.Api.Middleware;

/// <summary>
/// Maps Application-layer exceptions to HTTP responses in one place, so
/// controllers stay free of repetitive try/catch blocks.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, message) = ex switch
            {
                EntityNotFoundException => (HttpStatusCode.NotFound, ex.Message),
                WeekLockedException => (HttpStatusCode.Conflict, ex.Message),
                BusinessRuleException => (HttpStatusCode.BadRequest, ex.Message),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
            };

            if (status == HttpStatusCode.InternalServerError)
                logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)status;
            // In Development only, unexpected errors also send back the real
            // error (including any inner SQL error) so it can be read in the
            // browser's Network tab. Production still gets the generic message.
            var detail = status == HttpStatusCode.InternalServerError && environment.IsDevelopment()
                ? ex.ToString()
                : null;
            await context.Response.WriteAsJsonAsync(new { status = (int)status, title = message, detail });
        }
    }
}
