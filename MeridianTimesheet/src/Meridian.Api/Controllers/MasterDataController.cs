using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>Reference data for the frontend's dropdowns/filters (open to any
/// authenticated user) plus the Master Data admin screen's mutations
/// (Accounts/Projects/Modules/Tasks/Holidays — Admin role only).
/// Departments/Locations stay read-only everywhere: they're sourced from
/// the real org chart, not something this application should hand-edit.</summary>
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class MasterDataController(IMasterDataService masterDataService, ICurrentUserService currentUser) : ControllerBase
{
	// ---- Reads (any authenticated user) ----

	[HttpGet("departments")]
	public async Task<IActionResult> GetDepartments(CancellationToken ct) =>
		Ok(await masterDataService.GetDepartmentsAsync(ct));

	[HttpGet("locations")]
	public async Task<IActionResult> GetLocations(CancellationToken ct) =>
		Ok(await masterDataService.GetLocationsAsync(ct));

	[HttpGet("accounts")]
	public async Task<IActionResult> GetAccounts(CancellationToken ct) =>
		Ok(await masterDataService.GetAccountsAsync(ct));

	[HttpGet("projects")]
	public async Task<IActionResult> GetProjects(CancellationToken ct) =>
		Ok(await masterDataService.GetProjectsAsync(ct));

	[HttpGet("modules")]
	public async Task<IActionResult> GetModules([FromQuery] int? projectId, CancellationToken ct) =>
		Ok(await masterDataService.GetModulesAsync(projectId, ct));

	[HttpGet("tasks")]
	public async Task<IActionResult> GetTasks([FromQuery] int? moduleId, CancellationToken ct) =>
		Ok(await masterDataService.GetTasksAsync(moduleId, ct));

	[HttpGet("holidays")]
	public async Task<IActionResult> GetHolidays(CancellationToken ct) =>
		Ok(await masterDataService.GetHolidaysAsync(ct));

	[HttpGet("task-categories")]
	public async Task<IActionResult> GetTaskCategories(CancellationToken ct) =>
		Ok(await masterDataService.GetTaskCategoriesAsync(ct));

	// ---- Mutations (Admin role only) ----

	[HttpPost("accounts")]
	public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.CreateAccountAsync(request, ct));
	}

	[HttpPut("accounts/{accountId:int}")]
	public async Task<IActionResult> UpdateAccount(int accountId, [FromBody] UpdateAccountRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.UpdateAccountAsync(accountId, request, ct));
	}

	[HttpPost("projects")]
	public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.CreateProjectAsync(request, ct));
	}

	[HttpPut("projects/{projectId:int}")]
	public async Task<IActionResult> UpdateProject(int projectId, [FromBody] UpdateProjectRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.UpdateProjectAsync(projectId, request, ct));
	}

	[HttpPost("modules")]
	public async Task<IActionResult> CreateModule([FromBody] CreateModuleRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.CreateModuleAsync(request, ct));
	}

	[HttpPut("modules/{moduleId:int}")]
	public async Task<IActionResult> UpdateModule(int moduleId, [FromBody] UpdateModuleRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.UpdateModuleAsync(moduleId, request, ct));
	}

	[HttpPost("tasks")]
	public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.CreateTaskAsync(request, ct));
	}

	[HttpPut("tasks/{taskId:int}")]
	public async Task<IActionResult> UpdateTask(int taskId, [FromBody] UpdateTaskRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.UpdateTaskAsync(taskId, request, ct));
	}

	[HttpPost("holidays")]
	public async Task<IActionResult> CreateHoliday([FromBody] CreateHolidayRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.CreateHolidayAsync(request, ct));
	}

	[HttpPut("holidays/{holidayId:int}")]
	public async Task<IActionResult> UpdateHoliday(int holidayId, [FromBody] UpdateHolidayRequest request, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.UpdateHolidayAsync(holidayId, request, ct));
	}

	[HttpDelete("holidays/{holidayId:int}")]
	public async Task<IActionResult> DeleteHoliday(int holidayId, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		await masterDataService.DeleteHolidayAsync(holidayId, ct);
		return NoContent();
	}
}