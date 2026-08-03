using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/employees")]
//[Authorize]
public class EmployeesController(IEmployeeService employeeService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>The calling employee's own record — the frontend uses this on load
    /// to figure out who's logged in and what they're allowed to see.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        if (currentUser.EmployeeCode is null) return Unauthorized();
        var employee = await employeeService.GetByCodeAsync(currentUser.EmployeeCode, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpGet("{employeeCode}")]
    public async Task<IActionResult> GetByCode(string employeeCode, CancellationToken ct)
    {
        var employee = await employeeService.GetByCodeAsync(employeeCode, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpGet("{employeeCode}/manager")]
    public async Task<IActionResult> GetManager(string employeeCode, CancellationToken ct)
    {
        var manager = await employeeService.GetManagerAsync(employeeCode, ct);
        return manager is null ? NotFound() : Ok(manager);
    }

    [HttpGet("{employeeCode}/direct-reports")]
    public async Task<IActionResult> GetDirectReports(string employeeCode, CancellationToken ct) =>
        Ok(await employeeService.GetDirectReportsAsync(employeeCode, ct));
}
