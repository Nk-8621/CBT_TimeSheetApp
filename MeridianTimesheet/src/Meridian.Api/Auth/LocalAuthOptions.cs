using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Meridian.Api.Auth;

/// <summary>
/// Settings for the app's own username/password admin login (section
/// "LocalAuth" in appsettings / environment variables). Microsoft Entra
/// sign-in has been removed; the API now issues and validates its own JWTs.
/// </summary>
public class LocalAuthOptions
{
    public const string SectionName = "LocalAuth";
    public const string Issuer = "meridian-timesheet";
    public const string Audience = "meridian-timesheet-web";
    public const string EmployeeCodeClaim = "employee_code";

    /// <summary>Admin login name.</summary>
    public string AdminUsername { get; set; } = "admin";

    /// <summary>Admin password. Login is refused while this is empty.</summary>
    public string AdminPassword { get; set; } = "";

    /// <summary>The Employee record the admin login acts as. It must be an
    /// active employee holding the ADMIN role.</summary>
    public string AdminEmployeeCode { get; set; } = "";

    /// <summary>Secret used to sign tokens (at least 32 characters). If
    /// empty, a random key is generated at startup, so tokens stop working
    /// whenever the API restarts.</summary>
    public string JwtSigningKey { get; set; } = "";

    public int TokenLifetimeHours { get; set; } = 8;

    private SymmetricSecurityKey? _key;

    public SymmetricSecurityKey GetSigningKey()
    {
        if (_key is not null) return _key;
        var bytes = string.IsNullOrWhiteSpace(JwtSigningKey) || JwtSigningKey.Length < 32
            ? RandomNumberGenerator.GetBytes(64)
            : Encoding.UTF8.GetBytes(JwtSigningKey);
        _key = new SymmetricSecurityKey(bytes);
        return _key;
    }
}
