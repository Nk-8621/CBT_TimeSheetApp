using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace Meridian.Api.Auth;

/// <summary>
/// Resolves the calling employee from the "employee_code" claim of the
/// API's own JWT (issued by AuthController at login). A deactivated
/// employee resolves to null (-> 401). Admin status is always checked
/// against the Carbynetech_EmployeeRole table, never trusted from claims.
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
        if (user?.Identity?.IsAuthenticated != true) return null;

        var code = user.FindFirst(LocalAuthOptions.EmployeeCodeClaim)?.Value;
        if (string.IsNullOrWhiteSpace(code)) return null;

        // Blocking call is acceptable - runs once per request and keeps
        // ICurrentUserService's synchronous property shape.
        var employee = employeeRepository.GetByCodeAsync(code).GetAwaiter().GetResult();
        return employee is { IsActive: true } ? employee.EmployeeCode : null;
    }

    private bool ResolveIsAdmin(IEmployeeRepository employeeRepository)
    {
        if (EmployeeCode is null) return false;
        var employee = employeeRepository.GetByCodeAsync(EmployeeCode).GetAwaiter().GetResult();
        if (employee is null) return false;
        return employeeRepository.HasRoleAsync(employee.EmployeeId, "ADMIN").GetAwaiter().GetResult();
    }
}
