using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>Shared "is this person allowed to touch this employee's data?" check —
/// self-service actions are only for yourself, unless you're an Admin.</summary>
public abstract class MeridianControllerBase(ICurrentUserService currentUser) : ControllerBase
{
    protected ICurrentUserService CurrentUser => currentUser;

    protected IActionResult? EnsureSelfOrAdmin(string employeeCode)
    {
        if (currentUser.EmployeeCode is null) return Unauthorized();
        if (currentUser.IsAdmin) return null;
        if (!string.Equals(currentUser.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase))
            return Forbid();
        return null;
    }
}
