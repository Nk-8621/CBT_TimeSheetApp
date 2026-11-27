using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Meridian.Api.Auth;

/// <summary>
/// DRY-RUN / LOCAL TESTING ONLY - never active on the live site.
///
/// Lets a tester sign in as any employee by sending the employee code in the
/// <c>X-Dev-Employee-Code</c> request header, with no Microsoft sign-in.
/// It is registered in Program.cs only when ALL of these are true:
///   * the app runs with ASPNETCORE_ENVIRONMENT = Development, and
///   * configuration "Authentication:DevMode" = true
///     (set by the "DryRunTestLogin" profile in launchSettings.json - it is
///     deliberately NOT in any appsettings*.json file).
/// Even then, every request is rejected unless it comes from this machine
/// (loopback address, Host = localhost/127.0.0.1, no proxy forwarding headers).
/// When the header is absent the normal Entra bearer-token check is used.
/// </summary>
public sealed class DevLoginAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevLogin";
    public const string PolicySchemeName = "DevLoginOrEntra";
    public const string HeaderName = "X-Dev-Employee-Code";
    public const string EmployeeCodeClaim = "meridian_dev_employee_code";
    public const string ConfigKey = "Authentication:DevMode";

    private static readonly string[] ForwardingHeaders =
        ["X-Forwarded-For", "X-Forwarded-Host", "X-Original-For", "X-Real-IP", "Forwarded"];

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue)
            || string.IsNullOrWhiteSpace(headerValue.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!IsLocalRequest(Context))
        {
            Logger.LogWarning("Dev login header rejected: request is not from localhost ({Remote}, Host {Host}).",
                Context.Connection.RemoteIpAddress, Request.Host.Host);
            return Task.FromResult(AuthenticateResult.Fail("Dev login is only accepted from localhost."));
        }

        var employeeCode = headerValue.ToString().Trim();
        if (employeeCode.Length > 50)
            return Task.FromResult(AuthenticateResult.Fail("Invalid employee code."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, employeeCode),
            new Claim(ClaimTypes.Name, employeeCode),
            new Claim(EmployeeCodeClaim, employeeCode),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool IsLocalRequest(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null || !IPAddress.IsLoopback(remote)) return false;

        var host = context.Request.Host.Host;
        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            && host != "127.0.0.1" && host != "[::1]" && host != "::1")
            return false;

        foreach (var header in ForwardingHeaders)
            if (context.Request.Headers.ContainsKey(header)) return false;

        return true;
    }
}
