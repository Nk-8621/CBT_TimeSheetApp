using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Meridian.Api.Auth;

/// <summary>
/// Resolves the calling employee from the current Entra access token's
/// claims. Fast path: the token's "oid" claim already matches an
/// Employee.EntraObjectId from a previous sign-in. First-ever sign-in for
/// an employee: no Employee row has that oid yet, so this falls back to
/// matching by email/UPN against an existing Employee record (created by
/// an admin via Master Data) and links that record's EntraObjectId to the
/// oid for next time (just-in-time linking, not employee creation - a
/// Microsoft account with no matching Employee record resolves to null
/// here, which the caller treats as "not a recognized employee").
/// Admin status is always checked against the database's
/// Carbynetech_EmployeeRole table, never trusted from claims, since Entra
/// App Roles aren't configured for this app.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly Lazy<string?> _employeeCode;
    private readonly Lazy<bool> _isAdmin;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IEmployeeRepository employeeRepository)
    {
        _employeeCode = new Lazy<string?>(() => ResolveEmployeeCode(httpContextAccessor, employeeRepository));
        _isAdmin = new Lazy<bool>(() => ResolveIsAdmin(employeeRepository));
    }

    public string? EmployeeCode => _employeeCode.Value;
    public bool IsAdmin => _isAdmin.Value;

    private static string? ResolveEmployeeCode(IHttpContextAccessor accessor, IEmployeeRepository employeeRepository)
    {
        var user = accessor.HttpContext?.User;
        if (user is null) return null;

        var oidClaim = user.FindFirst("oid")?.Value ?? user.FindFirst(
            "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        if (oidClaim is null || !Guid.TryParse(oidClaim, out var oid)) return null;

        // Blocking calls are acceptable here — this runs once per request,
        // early, and keeps ICurrentUserService's synchronous property shape
        // that controllers/services depend on throughout.
        var employee = employeeRepository.GetByEntraObjectIdAsync(oid).GetAwaiter().GetResult();
        if (employee is not null) return employee.EmployeeCode;

        // First sign-in for this Entra identity — no Employee row has this
        // oid yet. Fall back to matching by email/UPN against an existing
        // Employee record, and link it for next time.
        var email = user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst("upn")?.Value
            ?? user.FindFirst(ClaimTypes.Upn)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
        if (string.IsNullOrWhiteSpace(email)) return null;

        var matchedByEmail = employeeRepository.GetByCodeOrEmailAsync(email).GetAwaiter().GetResult();
        if (matchedByEmail is null) return null; // No employee record for this Microsoft account - not auto-created.

        matchedByEmail.EntraObjectId = oid;
        employeeRepository.SaveChangesAsync().GetAwaiter().GetResult();
        return matchedByEmail.EmployeeCode;
    }

    private bool ResolveIsAdmin(IEmployeeRepository employeeRepository)
    {
        if (EmployeeCode is null) return false;
        var employee = employeeRepository.GetByCodeAsync(EmployeeCode).GetAwaiter().GetResult();
        if (employee is null) return false;
        return employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN").GetAwaiter().GetResult();
    }
}
