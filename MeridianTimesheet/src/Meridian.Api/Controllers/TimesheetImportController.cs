using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>
/// Kept separate from the main TimesheetController rather than adding a
/// method there — file upload has a different request shape (multipart
/// form, not JSON) and this can grow (e.g. a "download the template"
/// endpoint later) without cluttering the main timesheet CRUD surface.
/// </summary>
[ApiController]
[Route("api/timesheet-import")]
[Authorize]
public class TimesheetImportController(ITimesheetExcelImportService importService, ICurrentUserService currentUser) : ControllerBase
{
	/// <summary>Downloads the blank .xlsx template — same file shape ImportWeek expects back.</summary>
	[HttpGet("template")]
	public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
	{
		var bytes = await importService.GenerateTemplateAsync(ct);
		return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Meridian_Timesheet_Import_Template.xlsx");
	}

	/// <summary>Imports one week's task lines from an uploaded Excel file —
	/// self-service only, an employee can only import their own data.</summary>
	[HttpPost("{employeeCode}/{weekStart}")]
	public async Task<IActionResult> ImportWeek(string employeeCode, DateOnly weekStart, IFormFile file, CancellationToken ct)
	{
		// ASSUMPTION — matches the same "self or admin" check the rest of
		// the timesheet endpoints presumably already use; adjust if yours
		// is named or structured differently.
		if (!string.Equals(currentUser.EmployeeCode, employeeCode, StringComparison.OrdinalIgnoreCase) && !currentUser.IsAdmin)
			return Forbid();

		if (file is null || file.Length == 0)
			return BadRequest(new { title = "No file was uploaded." });

		await using var stream = file.OpenReadStream();
		var result = await importService.ImportWeekAsync(employeeCode, weekStart, stream, ct);
		return Ok(result);
	}
}