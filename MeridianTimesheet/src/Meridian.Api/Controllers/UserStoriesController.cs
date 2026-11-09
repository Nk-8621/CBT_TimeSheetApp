using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/user-stories")]
[Authorize]
public class UserStoriesController(IUserStoryService userStoryService, ICurrentUserService currentUser)
	: MeridianControllerBase(currentUser)
{
	[HttpGet("{employeeCode}")]
	public async Task<IActionResult> GetForEmployee(string employeeCode, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await userStoryService.GetForEmployeeAsync(employeeCode, ct));
	}

	[HttpPost("{employeeCode}")]
	public async Task<IActionResult> Create(string employeeCode, [FromBody] CreateUserStoryRequest request, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		var created = await userStoryService.CreateAsync(employeeCode, request, ct);
		return CreatedAtAction(nameof(GetForEmployee), new { employeeCode }, created);
	}
}
