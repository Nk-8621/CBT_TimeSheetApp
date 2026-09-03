using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/day-type-requests")]
[Authorize]
public class DayTypeRequestsController(IDayTypeRequestService dayTypeRequestService, ICurrentUserService currentUser)
	: MeridianControllerBase(currentUser)
{
	/// <summary>This employee's own WFH/Leave requests, newest first.</summary>
	[HttpGet("{employeeCode}")]
	public async Task<IActionResult> GetMine(string employeeCode, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await dayTypeRequestService.GetMyRequestsAsync(employeeCode, ct));
	}

	[HttpPost("{employeeCode}")]
	public async Task<IActionResult> Submit(string employeeCode, [FromBody] CreateDayTypeRequestRequest request, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await dayTypeRequestService.SubmitAsync(employeeCode, request.Date, request.RequestType, request.Note, ct));
	}

	/// <summary>The calling manager's own Level 1 inbox - requests pending from
	/// their direct reports.</summary>
	[HttpGet("queue")]
	public async Task<IActionResult> GetQueue(CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await dayTypeRequestService.GetApprovalQueueAsync(CurrentUser.EmployeeCode, ct));
	}

	/// <summary>Approves as the CALLER - same rule as week approvals: you can
	/// only approve using your own identity.</summary>
	[HttpPost("{requestId:int}/approve")]
	public async Task<IActionResult> Approve(int requestId, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await dayTypeRequestService.ApproveAsync(requestId, CurrentUser.EmployeeCode, ct));
	}

	[HttpPost("{requestId:int}/reject")]
	public async Task<IActionResult> Reject(int requestId, [FromBody] DecideDayTypeRequestRequest request, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await dayTypeRequestService.RejectAsync(requestId, CurrentUser.EmployeeCode, request.Comment, ct));
	}
}
