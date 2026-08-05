using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(IReportsService reportsService, IEmployeeService employeeService, ICurrentUserService currentUser) : ControllerBase
{
	/// <summary>Hours by project for one week — Admin sees the whole
	/// organization, everyone else sees their own direct reports.</summary>
	[HttpGet("project-hours")]
	public async Task<IActionResult> GetProjectHours([FromQuery] DateOnly weekStart, CancellationToken ct)
	{
		if (currentUser.EmployeeCode is null) return Unauthorized();

		var scopeEmployees = currentUser.IsAdmin
			? await employeeService.GetAllAsync(ct)
			: await employeeService.GetDirectReportsAsync(currentUser.EmployeeCode, ct);

		var employeeIds = scopeEmployees.Select(e => e.Id).ToList();
		return Ok(await reportsService.GetProjectHoursAsync(employeeIds, weekStart, ct));
	}

	/// <summary>The full Reports screen: all 5 rollup dimensions (department,
	/// customer/internal, project, resource, module &amp; task), filterable by
	/// week range, department, and approval status. Admin sees the whole
	/// organization, everyone else sees their own direct reports.</summary>
	[HttpGet("summary")]
	public async Task<IActionResult> GetSummary(
		[FromQuery] DateOnly weekFrom, [FromQuery] DateOnly weekTo,
		[FromQuery] int? departmentId, [FromQuery] string approvalStatus, CancellationToken ct)
	{
		if (currentUser.EmployeeCode is null) return Unauthorized();

		var scopeEmployees = currentUser.IsAdmin
			? await employeeService.GetAllAsync(ct)
			: await employeeService.GetDirectReportsAsync(currentUser.EmployeeCode, ct);

		var employeeIds = scopeEmployees.Select(e => e.Id).ToList();
		return Ok(await reportsService.GetSummaryAsync(employeeIds, weekFrom, weekTo, departmentId, approvalStatus, ct));
	}
}