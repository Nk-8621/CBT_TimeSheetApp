using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/timesheet")]
[Authorize]
public class TimesheetController(ITimesheetService timesheetService, ICurrentUserService currentUser)
	: MeridianControllerBase(currentUser)
{
	// Route constrained to look like a date (YYYY-MM-DD) so it can never
	
	[HttpGet("{employeeCode}/{weekStart:regex(^\\d{{4}}-\\d{{2}}-\\d{{2}}$)}")]
	public async Task<IActionResult> GetWeek(string employeeCode, DateOnly weekStart, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await timesheetService.GetWeekAsync(employeeCode, weekStart, ct));
	}

	[HttpGet("{employeeCode}/history")]
	public async Task<IActionResult> GetHistory(string employeeCode, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await timesheetService.GetHistoryAsync(employeeCode, ct));
	}

	[HttpPost("{employeeCode}/{weekStart}/entries")]
	public async Task<IActionResult> AddEntry(string employeeCode, DateOnly weekStart, [FromBody] CreateTimeEntryRequest request, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		var created = await timesheetService.AddEntryAsync(employeeCode, weekStart, request, ct);
		return CreatedAtAction(nameof(GetWeek), new { employeeCode, weekStart = weekStart.ToString("yyyy-MM-dd") }, created);
	}

	[HttpPut("entries/{timeEntryId:int}")]
	public async Task<IActionResult> UpdateEntry(int timeEntryId, [FromBody] UpdateTimeEntryRequest request, CancellationToken ct)
	{
		if (await EnsureEntryOwnerOrAdminAsync(timeEntryId, ct) is IActionResult denied) return denied;
		return Ok(await timesheetService.UpdateEntryAsync(timeEntryId, request, ct));
	}

	[HttpDelete("entries/{timeEntryId:int}")]
	public async Task<IActionResult> RemoveEntry(int timeEntryId, CancellationToken ct)
	{
		if (await EnsureEntryOwnerOrAdminAsync(timeEntryId, ct) is IActionResult denied) return denied;
		await timesheetService.RemoveEntryAsync(timeEntryId, ct);
		return NoContent();
	}

	/// <summary>These two routes carry only the entry id, not an employeeCode,
	/// so the usual EnsureSelfOrAdmin check needs the entry's owner looked up
	/// first. An unknown id falls through to the service, which returns 404.</summary>
	private async Task<IActionResult?> EnsureEntryOwnerOrAdminAsync(int timeEntryId, CancellationToken ct)
	{
		var ownerCode = await timesheetService.GetEntryOwnerCodeAsync(timeEntryId, ct);
		return ownerCode is null ? null : EnsureSelfOrAdmin(ownerCode);
	}

	[HttpPut("{employeeCode}/day-type/{date}")]
	public async Task<IActionResult> SetDayType(string employeeCode, DateOnly date, [FromBody] SetDayTypeRequest request, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await timesheetService.SetDayTypeAsync(employeeCode, date, request.DayType, ct));
	}

	[HttpPost("{employeeCode}/{weekStart}/copy-last-week")]
	public async Task<IActionResult> CopyLastWeek(string employeeCode, DateOnly weekStart, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		var count = await timesheetService.CopyLastWeekAsync(employeeCode, weekStart, ct);
		return Ok(new { linesAdded = count });
	}
}
