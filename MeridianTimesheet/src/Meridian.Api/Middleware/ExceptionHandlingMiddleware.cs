using System.Net;
using Meridian.Application.Exceptions;

namespace Meridian.Api.Middleware;

/// <summary>
/// Maps Application-layer exceptions to HTTP responses in one place, so
/// controllers stay free of repetitive try/catch blocks.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IConfiguration configuration)
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
			// TEMPORARY DIAGNOSTICS: set "Diagnostics": { "ShowErrorDetails": true }
			// in appsettings (e.g. appsettings.Production.json on the server) to
			// send the real error back in the response's "detail" field. Turn it
			// off again afterwards - it exposes internal details to the browser.
			var detail = status == HttpStatusCode.InternalServerError
						 && configuration.GetValue<bool>("Diagnostics:ShowErrorDetails")
				? ex.ToString()
				: null;
			await context.Response.WriteAsJsonAsync(new { status = (int)status, title = message, detail });
		}
	}
}
