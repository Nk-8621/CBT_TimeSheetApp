using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>PROJECT STORIES (Nov 2026): user story names a Level 1 manager
/// (for projects allocated to him) or an Admin (any project) adds, which
/// everyone allocated to the project can pick when creating a user story.
/// Always acts as the signed-in user.</summary>
[ApiController]
[Route("api/project-stories")]
[Authorize]
public class ProjectStoriesController(IUserStoryService userStoryService, ICurrentUserService currentUser)
	: MeridianControllerBase(currentUser)
{
	/// <summary>Projects the signed-in user can add story names for, with their names.</summary>
	[HttpGet("managed")]
	public async Task<IActionResult> GetManaged(CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is not string code) return Unauthorized();
		return Ok(await userStoryService.GetManagedProjectsAsync(code, CurrentUser.IsAdmin, ct));
	}

	/// <summary>Open story names of the signed-in user's allocated projects.</summary>
	[HttpGet("available")]
	public async Task<IActionResult> GetAvailable(CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is not string code) return Unauthorized();
		return Ok(await userStoryService.GetAvailableProjectStoryNamesAsync(code, ct));
	}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] CreateProjectStoryNameRequest request, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is not string code) return Unauthorized();
		return Ok(await userStoryService.AddProjectStoryNameAsync(code, CurrentUser.IsAdmin, request, ct));
	}

	[HttpPut("{projectStoryNameId:int}")]
	public async Task<IActionResult> Update(int projectStoryNameId, [FromBody] UpdateProjectStoryNameRequest request, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is not string code) return Unauthorized();
		return Ok(await userStoryService.UpdateProjectStoryNameAsync(code, CurrentUser.IsAdmin, projectStoryNameId, request, ct));
	}
}
