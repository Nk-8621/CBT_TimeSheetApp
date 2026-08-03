using System.Net;
using Meridian.Application.Exceptions;

namespace Meridian.Api.Middleware;

/// <summary>
/// Maps Application-layer exceptions to HTTP responses in one place, so
/// controllers stay free of repetitive try/catch blocks.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            await context.Response.WriteAsJsonAsync(new { status = (int)status, title = message });
        }
    }
}
