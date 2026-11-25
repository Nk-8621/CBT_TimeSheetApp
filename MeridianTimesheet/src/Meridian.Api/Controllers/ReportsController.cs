using Meridian.Application.Interfaces.Services;
using Meridian.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(IReportsService reportsService, IEmployeeService employeeService, IMasterDataService masterDataService, ICurrentUserService currentUser) : ControllerBase
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
		[FromQuery] int? departmentId, [FromQuery] string approvalStatus, CancellationToken ct,
		// DATE RANGE (Nov 2026): optional exact From/To dates from the Reports page.
		// weekFrom/weekTo are still sent (the Mondays of those dates). To revert: remove these two.
		[FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
	{
		if (currentUser.EmployeeCode is null) return Unauthorized();

		var scopeEmployees = currentUser.IsAdmin
			? await employeeService.GetAllAsync(ct)
			: await employeeService.GetDirectReportsAsync(currentUser.EmployeeCode, ct);

		var employeeIds = scopeEmployees.Select(e => e.Id).ToList();
		return Ok(await reportsService.GetSummaryAsync(employeeIds, weekFrom, weekTo, departmentId, approvalStatus, ct, from, to));
	}

	/// <summary>REPORTS DOWNLOAD (Nov 2026): the same report as /summary, as an
	/// Excel file (Summary + Department-wise + Customer-Internal + Project-wise
	/// sheets). Takes exactly the same filters and the same access rules.</summary>
	[HttpGet("summary/export")]
	public async Task<IActionResult> ExportSummary(
		[FromQuery] DateOnly weekFrom, [FromQuery] DateOnly weekTo,
		[FromQuery] int? departmentId, [FromQuery] string approvalStatus, CancellationToken ct,
		[FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
	{
		if (currentUser.EmployeeCode is null) return Unauthorized();

		var scopeEmployees = currentUser.IsAdmin
			? await employeeService.GetAllAsync(ct)
			: await employeeService.GetDirectReportsAsync(currentUser.EmployeeCode, ct);

		var employeeIds = scopeEmployees.Select(e => e.Id).ToList();
		var summary = await reportsService.GetSummaryAsync(employeeIds, weekFrom, weekTo, departmentId, approvalStatus, ct, from, to);

		// The details shown at the top of the Summary sheet.
		var fromDate = from ?? weekFrom;
		var toDate = to ?? weekTo.AddDays(6);
		var period = $"{fromDate:dd MMM yyyy} – {toDate:dd MMM yyyy}";

		var departmentName = "All departments";
		if (departmentId is int id)
		{
			var departments = await masterDataService.GetDepartmentsAsync(ct);
			departmentName = departments.FirstOrDefault(d => d.Id == id)?.Name ?? "All departments";
		}

		var statusLabel = approvalStatus switch
		{
			"sub" => "Submitted",
			"ok" => "Approved",
			_ => "All Timesheets",
		};

		var me = await employeeService.GetByCodeAsync(currentUser.EmployeeCode, ct);
		var generatedBy = me?.FullName ?? currentUser.EmployeeCode;

		var bytes = ReportsExcelBuilder.Build(summary, period, departmentName, statusLabel, generatedBy, DateTime.Now);
		var fileName = $"Hours-Report-{fromDate:yyyy-MM-dd}-to-{toDate:yyyy-MM-dd}.xlsx";
		return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
	}
}