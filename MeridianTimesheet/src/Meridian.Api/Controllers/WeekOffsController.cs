using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>
/// WEEK OFFS (Nov 2026): Master Data -> Week Offs. Admin only, like the
/// rest of Master Data - every endpoint checks that first.
/// </summary>
[ApiController]
[Route("api/week-offs")]
[Authorize]
public class WeekOffsController(IWeekOffService weekOffService, ICurrentUserService currentUser) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> GetAll(CancellationToken ct)
	{
		if (!currentUser.IsAdmin || currentUser.EmployeeCode is null) return Forbid();
		return Ok(await weekOffService.GetAllAsync(ct));
	}

	/// <summary>Employees and clients for the Add Week Off drawer.</summary>
	[HttpGet("options")]
	public async Task<IActionResult> GetOptions(CancellationToken ct)
	{
		if (!currentUser.IsAdmin || currentUser.EmployeeCode is null) return Forbid();
		return Ok(await weekOffService.GetOptionsAsync(ct));
	}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] SaveEmployeeWeekOffRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin || currentUser.EmployeeCode is null) return Forbid();
		return Ok(await weekOffService.CreateAsync(request, currentUser.EmployeeCode!, ct));
	}

	[HttpPut("{id:int}")]
	public async Task<IActionResult> Update(int id, [FromBody] SaveEmployeeWeekOffRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin || currentUser.EmployeeCode is null) return Forbid();
		return Ok(await weekOffService.UpdateAsync(id, request, ct));
	}

	[HttpDelete("{id:int}")]
	public async Task<IActionResult> Delete(int id, CancellationToken ct)
	{
		if (!currentUser.IsAdmin || currentUser.EmployeeCode is null) return Forbid();
		await weekOffService.DeleteAsync(id, ct);
		return NoContent();
	}

	[HttpGet("template")]
	public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
	{
		if (!currentUser.IsAdmin || currentUser.EmployeeCode is null) return Forbid();
		var bytes = await weekOffService.GenerateTemplateAsync(ct);
		return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Week_Offs_Template.xlsx");
	}

	/// <summary>Bulk upload - nothing is saved if any row has a problem.</summary>
	[HttpPost("import")]
	public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
	{
		if (!currentUser.IsAdmin || currentUser.EmployeeCode is null) return Forbid();
		if (file is null || file.Length == 0)
			return BadRequest(new { title = "No file was uploaded." });

		await using var stream = file.OpenReadStream();
		return Ok(await weekOffService.ImportAsync(stream, currentUser.EmployeeCode!, ct));
	}
}
