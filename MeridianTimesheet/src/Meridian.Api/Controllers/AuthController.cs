using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

/// <summary>
/// Deliberately simple, dev-phase login: one shared password for every
/// employee, no hashing, no session tokens — the frontend just remembers
/// which employee code logged in and keeps sending it as the dev-mode
/// auth header (see api/authBridge.ts). This is NOT production-grade
/// authentication; it exists to give a real "type your ID, see only your
/// own screens" experience ahead of wiring up actual Microsoft Entra login.
/// No [Authorize] here deliberately — this must be reachable before any
/// identity exists yet.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(IEmployeeService employeeService) : ControllerBase
{
	private const string SharedPassword = "Carbynetech@123";

	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(request.EmployeeCode) || request.Password != SharedPassword)
			return Unauthorized(new { title = "Incorrect employee ID or password." });

		var employee = await employeeService.GetByCodeAsync(request.EmployeeCode.Trim(), ct);
		if (employee is null)
			return Unauthorized(new { title = "Incorrect employee ID or password." });

		return Ok(employee);
	}
}