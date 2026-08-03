namespace Meridian.Application.Interfaces.Services;

/// <summary>
/// Resolves who is actually making this request. Implemented in the API
/// layer (it needs HttpContext), kept as an interface here so services can
/// depend on "who is calling" without depending on ASP.NET Core itself.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>The calling employee's code (e.g. "CBT1267"), or null if unresolved.</summary>
    string? EmployeeCode { get; }

    /// <summary>True if the caller holds the ADMIN product-access role — allowed to
    /// act on behalf of others for support/setup purposes.</summary>
    bool IsAdmin { get; }
}
