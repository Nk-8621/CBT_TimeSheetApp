using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meridian.Api.Controllers;

[ApiController]
[Route("api/approvals")]
[Authorize]
public class ApprovalsController(IWeekApprovalService approvalService, ICurrentUserService currentUser)
	: MeridianControllerBase(currentUser)
{
	[HttpGet("validate/{employeeCode}/{weekStart}")]
	public async Task<IActionResult> Validate(string employeeCode, DateOnly weekStart, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await approvalService.ValidateForSubmissionAsync(employeeCode, weekStart, ct));
	}

	[HttpPost("{employeeCode}/{weekStart}/submit")]
	public async Task<IActionResult> Submit(string employeeCode, DateOnly weekStart, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await approvalService.SubmitAsync(employeeCode, weekStart, ct));
	}

	[HttpPost("{employeeCode}/{weekStart}/recall")]
	public async Task<IActionResult> Recall(string employeeCode, DateOnly weekStart, CancellationToken ct)
	{
		if (EnsureSelfOrAdmin(employeeCode) is IActionResult denied) return denied;
		return Ok(await approvalService.RecallAsync(employeeCode, weekStart, ct));
	}

	/// <summary>Approves as the CALLER — you can only approve using your own
	/// identity, never on someone else's behalf (Admin included).</summary>
	[HttpPost("{employeeCode}/{weekStart}/approve-level1")]
	public async Task<IActionResult> ApproveLevel1(string employeeCode, DateOnly weekStart, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await approvalService.ApproveLevel1Async(employeeCode, weekStart, CurrentUser.EmployeeCode, ct));
	}

	[HttpPost("{employeeCode}/{weekStart}/approve-level2")]
	public async Task<IActionResult> ApproveLevel2(string employeeCode, DateOnly weekStart, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await approvalService.ApproveLevel2Async(employeeCode, weekStart, CurrentUser.EmployeeCode, ct));
	}

	[HttpPost("{employeeCode}/{weekStart}/reject")]
	public async Task<IActionResult> Reject(string employeeCode, DateOnly weekStart, [FromBody] RejectWeekRequest request, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await approvalService.RejectAsync(employeeCode, weekStart, CurrentUser.EmployeeCode, request.Reason, ct));
	}

	/// <summary>The calling approver's own pending queue (their Level 1 or Level 2 inbox).</summary>
	[HttpGet("pending")]
	public async Task<IActionResult> GetPending([FromQuery] bool level2, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await approvalService.GetPendingForApproverAsync(CurrentUser.EmployeeCode, level2, ct));
	}

	/// <summary>The full-detail queue — flags, line-level detail, billable split —
	/// for the Approvals screen. Sorted flagged items first.</summary>
	[HttpGet("queue")]
	public async Task<IActionResult> GetQueue([FromQuery] bool level2, CancellationToken ct)
	{
		if (CurrentUser.EmployeeCode is null) return Unauthorized();
		return Ok(await approvalService.GetApprovalQueueAsync(CurrentUser.EmployeeCode, level2, ct));
	}
}