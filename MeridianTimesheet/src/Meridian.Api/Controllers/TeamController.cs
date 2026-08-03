using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers
{
	[ApiController]
	[Route("api/team")]
	[Authorize]
	public class TeamController(ITeamService teamService, ICurrentUserService currentUser) : ControllerBase
	{
		/// <summary>The calling manager's own direct reports.</summary>
		[HttpGet("compliance")]
		public async Task<IActionResult> GetCompliance([FromQuery] DateOnly weekStart, CancellationToken ct)
		{
			if (currentUser.EmployeeCode is null) return Unauthorized();
			return Ok(await teamService.GetComplianceForManagerAsync(currentUser.EmployeeCode, weekStart, ct));
		}

		/// <summary>Everyone in the organization — Admin only.</summary>
		[HttpGet("compliance/all")]
		public async Task<IActionResult> GetComplianceAll([FromQuery] DateOnly weekStart, CancellationToken ct)
		{
			if (!currentUser.IsAdmin) return Forbid();
			return Ok(await teamService.GetComplianceForAllAsync(weekStart, ct));
		}
	}
}
