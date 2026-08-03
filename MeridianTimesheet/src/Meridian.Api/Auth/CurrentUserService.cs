using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace Meridian.Api.Auth;

/// <summary>
/// Resolves the calling employee from claims. Works with both the dev-mode
/// header claim ("employee_code") and a real Entra token's "oid" claim
/// (looked up against Employee.EntraObjectId). Admin status is always
/// checked against the database's Carbynetech_EmployeeRole table — not
/// trusted from claims — since Entra App Roles aren't configured yet.
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

        var devClaim = user.FindFirst("employee_code")?.Value;
        if (devClaim is not null) return devClaim;

        var oidClaim = user.FindFirst("oid")?.Value ?? user.FindFirst(
            "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        if (oidClaim is null || !Guid.TryParse(oidClaim, out var oid)) return null;

        // Blocking call is acceptable here — this runs once per request,
        // early, and keeps ICurrentUserService's synchronous property shape
        // that controllers/services depend on throughout.
        var employee = employeeRepository.GetByEntraObjectIdAsync(oid).GetAwaiter().GetResult();
        return employee?.EmployeeCode;
    }

    private bool ResolveIsAdmin(IEmployeeRepository employeeRepository)
    {
        if (EmployeeCode is null) return false;
        var employee = employeeRepository.GetByCodeAsync(EmployeeCode).GetAwaiter().GetResult();
        if (employee is null) return false;
        return employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN").GetAwaiter().GetResult();
    }
}
