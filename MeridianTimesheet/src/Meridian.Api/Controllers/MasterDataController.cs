using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>Read-only reference data for the frontend's dropdowns/filters.</summary>
[ApiController]
[Route("api/masterdata")]
//[Authorize]
public class MasterDataController(IMasterDataService masterDataService) : ControllerBase
{
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
}
