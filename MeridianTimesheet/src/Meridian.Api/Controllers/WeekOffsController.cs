using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>
/// WEEK OFFS (Nov 2026): Master Data -> Week Offs. Admin only, like the
/// rest of Master Data - every endpoint checks that first.
/// Client week offs live under /clients, employee assignments under /employees.
/// </summary>
[ApiController]
[Route("api/week-offs")]
[Authorize]
public class WeekOffsController(IWeekOffService weekOffService, ICurrentUserService currentUser) : ControllerBase
{
	private bool IsAllowed => currentUser.IsAdmin && currentUser.EmployeeCode is not null;

	/// <summary>Client week offs, each with its assigned employees.</summary>
	[HttpGet]
	public async Task<IActionResult> GetAll(CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		return Ok(await weekOffService.GetAllAsync(ct));
	}

	/// <summary>Employees and clients for the drawers.</summary>
	[HttpGet("options")]
	public async Task<IActionResult> GetOptions(CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		return Ok(await weekOffService.GetOptionsAsync(ct));
	}

	// ---- Client week offs ----

	[HttpPost("clients")]
	public async Task<IActionResult> CreateClientWeekOff([FromBody] SaveClientWeekOffRequest request, CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		await weekOffService.CreateClientWeekOffAsync(request, currentUser.EmployeeCode!, ct);
		return NoContent();
	}

	[HttpPut("clients/{id:int}")]
	public async Task<IActionResult> UpdateClientWeekOff(int id, [FromBody] SaveClientWeekOffRequest request, CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		await weekOffService.UpdateClientWeekOffAsync(id, request, ct);
		return NoContent();
	}

	[HttpDelete("clients/{id:int}")]
	public async Task<IActionResult> DeleteClientWeekOff(int id, CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		await weekOffService.DeleteClientWeekOffAsync(id, ct);
		return NoContent();
	}

	// ---- Employee assignments ----

	[HttpPost("employees")]
	public async Task<IActionResult> AssignEmployees([FromBody] AssignEmployeesRequest request, CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		await weekOffService.AssignEmployeesAsync(request, currentUser.EmployeeCode!, ct);
		return NoContent();
	}

	[HttpPut("employees/{id:int}")]
	public async Task<IActionResult> UpdateEmployeeWeekOff(int id, [FromBody] UpdateEmployeeWeekOffRequest request, CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		await weekOffService.UpdateEmployeeWeekOffAsync(id, request, ct);
		return NoContent();
	}

	[HttpDelete("employees/{id:int}")]
	public async Task<IActionResult> DeleteEmployeeWeekOff(int id, CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		await weekOffService.DeleteEmployeeWeekOffAsync(id, ct);
		return NoContent();
	}

	// ---- Excel (employee assignments) ----

	[HttpGet("template")]
	public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		var bytes = await weekOffService.GenerateTemplateAsync(ct);
		return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Week_Offs_Employees_Template.xlsx");
	}

	/// <summary>Bulk employee assignment - nothing is saved if any row has a problem.</summary>
	[HttpPost("import")]
	public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
	{
		if (!IsAllowed) return Forbid();
		if (file is null || file.Length == 0)
			return BadRequest(new { title = "No file was uploaded." });

		await using var stream = file.OpenReadStream();
		return Ok(await weekOffService.ImportAsync(stream, currentUser.EmployeeCode!, ct));
	}
}
