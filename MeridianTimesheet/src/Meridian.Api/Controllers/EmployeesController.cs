using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController(IEmployeeService employeeService, IAccessControlService accessControlService, ICurrentUserService currentUser) : ControllerBase
{
	/// <summary>Every employee — backs the Master Data "Resources" tab (read-only;
	/// employee data itself is real org-chart data, not editable from Meridian).</summary>
	[HttpGet]
	public async Task<IActionResult> GetAll(CancellationToken ct) =>
		Ok(await employeeService.GetAllAsync(ct));

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

	/// <summary>What this employee is allowed to see (RBAC), computed from
	/// their real position in the org hierarchy — see IAccessControlService.</summary>
	[HttpGet("{employeeCode}/access")]
	public async Task<IActionResult> GetAccess(string employeeCode, CancellationToken ct) =>
		Ok(await accessControlService.GetAccessProfileAsync(employeeCode, ct));
	

	/// <summary>Creates a new employee (internal or external) and immediately
/// grants portal access with the default password + forced first-login
/// flow. Admin-only.</summary>
[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		var employee = await employeeService.CreateEmployeeAsync(request, ct);
		return Ok(employee);
	}

	[HttpPut("{employeeCode}/primary-account")]
	public async Task<IActionResult> SetPrimaryAccount(string employeeCode, [FromBody] SetPrimaryAccountRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		await employeeService.SetPrimaryAccountAsync(employeeCode, request.AccountId, ct);
		return NoContent();
	}
}