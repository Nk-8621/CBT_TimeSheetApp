using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>Reference data for the frontend's dropdowns/filters (open to any
/// authenticated user) plus the Master Data admin screen's mutations
/// (Accounts/Projects/Modules/Tasks/Holidays/Project Types - Admin role
/// only). The "Others" quick-add endpoints are the one exception - any
/// authenticated employee can call those from Add Task Line, not just
/// admins. Departments/Locations stay read-only everywhere: they're sourced
/// from the real org chart, not something this application should hand-edit.</summary>
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

	[HttpGet("project-types")]
	public async Task<IActionResult> GetProjectTypes(CancellationToken ct) =>
		Ok(await masterDataService.GetProjectTypesAsync(ct));

	[HttpGet("project-types/with-templates")]
	public async Task<IActionResult> GetProjectTypesWithTemplates(CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.GetProjectTypesWithTemplatesAsync(ct));
	}

	[HttpGet("project-types/{projectTypeId:int}/template")]
	public async Task<IActionResult> GetProjectTypeWithTemplate(int projectTypeId, CancellationToken ct) =>
		Ok(await masterDataService.GetProjectTypeWithTemplateAsync(projectTypeId, ct));

	// ---- Project-wise resource allocation reporting (Admin only) ----

	[HttpGet("projects/resource-allocations")]
	public async Task<IActionResult> GetProjectResourceAllocations(CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.GetProjectResourceAllocationsAsync(ct));
	}

	[HttpGet("projects/{projectId:int}/allocated-employees")]
	public async Task<IActionResult> GetAllocatedEmployees(int projectId, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.GetAllocatedEmployeesAsync(projectId, ct));
	}

	// ---- Self-service "Others" quick-add (any authenticated user, from the timesheet entry screen) ----

	[HttpPost("projects/quick-add")]
	public async Task<IActionResult> QuickAddProject([FromBody] QuickAddProjectRequest request, CancellationToken ct) =>
		Ok(await masterDataService.QuickAddProjectAsync(request, ct));

	[HttpPost("modules/quick-add")]
	public async Task<IActionResult> QuickAddModule([FromBody] QuickAddModuleRequest request, CancellationToken ct) =>
		Ok(await masterDataService.QuickAddModuleAsync(request, ct));

	[HttpPost("tasks/quick-add")]
	public async Task<IActionResult> QuickAddTask([FromBody] QuickAddTaskRequest request, CancellationToken ct) =>
		Ok(await masterDataService.QuickAddTaskAsync(request, ct));

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

	// Explicit re-sync for a project that already has a Project Type set -
	// see IMasterDataService.SyncProjectModulesFromTemplateAsync's summary.
	[HttpPost("projects/{projectId:int}/sync-template")]
	public async Task<IActionResult> SyncProjectModulesFromTemplate(int projectId, CancellationToken ct)
	{
		if (!currentUser.IsAdmin) return Forbid();
		return Ok(await masterDataService.SyncProjectModulesFromTemplateAsync(projectId, ct));
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