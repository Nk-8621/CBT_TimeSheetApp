using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meridian.Api.Auth;

/// <summary>
/// Dev-mode authentication: trusts an "X-Dev-Employee-Code" header instead of
/// a real Microsoft Entra token. Mirrors the frontend's dev-mode fallback
/// (see MsalRoot/LoginGate) so the whole stack is runnable without a real
/// Azure AD tenant configured. MUST be disabled in production — see
/// Program.cs, which only registers this scheme when Authentication:DevMode
/// is explicitly true in configuration.
/// </summary>
public class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevAuth";
    public const string EmployeeCodeHeader = "X-Dev-Employee-Code";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(EmployeeCodeHeader, out var employeeCode) || string.IsNullOrWhiteSpace(employeeCode))
            return Task.FromResult(AuthenticateResult.Fail($"Missing '{EmployeeCodeHeader}' header (dev-mode auth is on)."));

        var claims = new[] { new Claim("employee_code", employeeCode.ToString()) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
