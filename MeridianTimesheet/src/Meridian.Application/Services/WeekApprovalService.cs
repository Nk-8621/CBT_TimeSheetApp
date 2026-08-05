using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class WeekApprovalService(
	IEmployeeRepository employeeRepository,
	ITimeEntryRepository timeEntryRepository,
	IDayTypeRepository dayTypeRepository,
	IWeekRecordRepository weekRecordRepository,
	IEmployeeService employeeService) : IWeekApprovalService
{
	public async Task<WeekValidationResult> ValidateForSubmissionAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var entries = await timeEntryRepository.GetForWeekAsync(employee.EmployeeId, weekStartDate, ct);
		var overrides = await dayTypeRepository.GetForWeekAsync(employee.EmployeeId, weekStartDate, ct);
		var overrideByDate = overrides.ToDictionary(o => o.EntryDate, o => o.DayType);

		var days = WeekMath.WeekDays(weekStartDate);
		var dayTypes = days.Select(d => overrideByDate.TryGetValue(d, out var t) ? t
			: WeekMath.IsWeekend(d) ? DayType.O : DayType.W).ToList();

		var lines = entries.Select(e => new ValidationLine(
			e.Task?.Name ?? $"Task #{e.TaskId}", e.Note, e.HoursByDay
		)).ToList();

		var (errors, warnings) = TimesheetValidator.Validate(weekStartDate, lines, dayTypes, DateOnly.FromDateTime(DateTime.UtcNow));
		return new WeekValidationResult(errors, warnings);
	}

	public async Task<WeekRecordDto> SubmitAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var validation = await ValidateForSubmissionAsync(employeeCode, weekStartDate, ct);
		if (!validation.CanSubmit)
			throw new BusinessRuleException("Cannot submit: " + string.Join(" ", validation.Errors));

		var week = await weekRecordRepository.GetOrCreateAsync(employee.EmployeeId, weekStartDate, ct);
		if (week.Status is not (WeekStatus.Draft or WeekStatus.Rejected))
			throw new WeekLockedException(employeeCode, weekStartDate, week.Status.ToString());

		var routing = await DetermineApprovalRoutingAsync(employeeCode, ct);
		week.SubmittedAt = DateTime.UtcNow;
		week.RejectedByEmployeeId = null;
		week.RejectionReason = null;
		week.UpdatedAt = DateTime.UtcNow;

		AddEvent(week, $"Submitted by {employee.FullName}", null, ApprovalEventStatus.Ok, employee.EmployeeId);

		if (routing.SkipLevel1)
		{
			// Direct manager has no manager of their own (e.g. reports straight
			// to an SVP) — there's no one to do a "Level 1" review, so this
			// goes straight to Level 2, with the direct manager approving it.
			week.Status = WeekStatus.PendingL2;
			AddEvent(week,
				routing.Level2Approver is not null
					? $"Awaiting Level 2 — {routing.Level2Approver.FullName} (reports directly to senior leadership; no Level 1 reviewer)"
					: "Awaiting Level 2 — no approver on file, contact an administrator",
				"Pending", ApprovalEventStatus.Pending, null);
		}
		else
		{
			week.Status = WeekStatus.PendingL1;
			AddEvent(week,
				routing.Level1Approver is not null ? $"Awaiting Level 1 — {routing.Level1Approver.FullName}" : "Awaiting Level 1 — no manager on file, contact an administrator",
				"Pending", ApprovalEventStatus.Pending, null);
		}

		await weekRecordRepository.SaveChangesAsync(ct);
		return ToDto(employeeCode, week);
	}

	public async Task<WeekRecordDto> RecallAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var week = await weekRecordRepository.GetOrCreateAsync(employee.EmployeeId, weekStartDate, ct);

		// A week that skipped Level 1 sits at PendingL2 immediately after
		// submission — recall must allow that case too, but only when no
		// actual "Level 1 approved" event exists in the trail (i.e. no one
		// has genuinely reviewed it yet). If it progressed normally through
		// a real Level 1 approval, PendingL2 means someone has already acted,
		// and it can no longer be recalled.
		var hasLevel1ApprovalEvent = week.ApprovalEvents.Any(e => e.EventText.StartsWith("Level 1 approved"));
		var canRecall = week.Status is WeekStatus.PendingL1 || (week.Status is WeekStatus.PendingL2 && !hasLevel1ApprovalEvent);
		if (!canRecall)
			throw new BusinessRuleException("Only a week awaiting approval, with no approval action taken yet, can be recalled.");

		week.Status = WeekStatus.Draft;
		week.SubmittedAt = null;
		week.UpdatedAt = DateTime.UtcNow;
		AddEvent(week, "Recalled by employee — back to draft", null, null, employee.EmployeeId);

		await weekRecordRepository.SaveChangesAsync(ct);
		return ToDto(employeeCode, week);
	}

	public async Task<WeekRecordDto> ApproveLevel1Async(string employeeCode, DateOnly weekStartDate, string approverEmployeeCode, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var week = await weekRecordRepository.GetOrCreateAsync(employee.EmployeeId, weekStartDate, ct);
		if (week.Status is not WeekStatus.PendingL1)
			throw new BusinessRuleException("This week isn't awaiting Level 1 approval.");

		await EnsureIsManagerAsync(employeeCode, approverEmployeeCode, ct);
		var approver = await RequireEmployeeAsync(approverEmployeeCode, ct);
		var l2Approver = await employeeService.GetSkipManagerAsync(employeeCode, ct);

		week.Status = WeekStatus.PendingL2;
		week.UpdatedAt = DateTime.UtcNow;
		AddEvent(week, $"Level 1 approved — {approver.FullName}", null, ApprovalEventStatus.Ok, approver.EmployeeId);
		AddEvent(week,
			l2Approver is not null ? $"Awaiting Level 2 — {l2Approver.FullName}" : "Awaiting Level 2 — no Level 2 approver on file",
			"Pending", ApprovalEventStatus.Pending, null);

		await weekRecordRepository.SaveChangesAsync(ct);
		return ToDto(employeeCode, week);
	}

	public async Task<WeekRecordDto> ApproveLevel2Async(string employeeCode, DateOnly weekStartDate, string approverEmployeeCode, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var week = await weekRecordRepository.GetOrCreateAsync(employee.EmployeeId, weekStartDate, ct);
		if (week.Status is not WeekStatus.PendingL2)
			throw new BusinessRuleException("This week isn't awaiting Level 2 approval.");

		await EnsureIsLevel2ApproverAsync(employeeCode, approverEmployeeCode, ct);
		var approver = await RequireEmployeeAsync(approverEmployeeCode, ct);

		week.Status = WeekStatus.Approved;
		week.UpdatedAt = DateTime.UtcNow;
		AddEvent(week, $"Level 2 approved — {approver.FullName}", null, ApprovalEventStatus.Ok, approver.EmployeeId);
		AddEvent(week, "Week locked", null, ApprovalEventStatus.Ok, null);

		await weekRecordRepository.SaveChangesAsync(ct);
		return ToDto(employeeCode, week);
	}

	public async Task<WeekRecordDto> RejectAsync(string employeeCode, DateOnly weekStartDate, string approverEmployeeCode, string reason, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var week = await weekRecordRepository.GetOrCreateAsync(employee.EmployeeId, weekStartDate, ct);
		if (week.Status is not (WeekStatus.PendingL1 or WeekStatus.PendingL2))
			throw new BusinessRuleException("Only a week pending approval can be returned.");

		if (week.Status is WeekStatus.PendingL1)
			await EnsureIsManagerAsync(employeeCode, approverEmployeeCode, ct);
		else
			await EnsureIsLevel2ApproverAsync(employeeCode, approverEmployeeCode, ct);

		var approver = await RequireEmployeeAsync(approverEmployeeCode, ct);

		week.Status = WeekStatus.Rejected;
		week.RejectedByEmployeeId = approver.EmployeeId;
		week.RejectionReason = reason;
		week.UpdatedAt = DateTime.UtcNow;
		AddEvent(week, $"Returned by {approver.FullName}", reason, ApprovalEventStatus.Rejected, approver.EmployeeId);

		await weekRecordRepository.SaveChangesAsync(ct);
		return ToDto(employeeCode, week);
	}

	public async Task<IReadOnlyList<WeekRecordDto>> GetPendingForApproverAsync(string approverEmployeeCode, bool level2, CancellationToken ct = default)
	{
		var status = level2 ? WeekStatus.PendingL2 : WeekStatus.PendingL1;
		var weeks = await weekRecordRepository.GetByStatusAsync(status, ct);

		var result = new List<WeekRecordDto>();
		foreach (var week in weeks)
		{
			var employee = await employeeRepository.GetByIdAsync(week.EmployeeId, ct);
			if (employee is null) continue;

			var relevantApprover = level2
				? (await DetermineApprovalRoutingAsync(employee.EmployeeCode, ct)).Level2Approver
				: await employeeService.GetManagerAsync(employee.EmployeeCode, ct);

			if (relevantApprover?.EmployeeCode == approverEmployeeCode)
				result.Add(ToDto(employee.EmployeeCode, week));
		}
		return result;
	}

	// ---- Shared helpers ----

	private record ApprovalRouting(bool SkipLevel1, EmployeeDto? Level1Approver, EmployeeDto? Level2Approver);

	/// <summary>Works out who approves at each level for this employee's
	/// submission — and whether Level 1 should be skipped entirely, which
	/// happens when the direct manager is themselves at the top of the
	/// hierarchy (no manager above them, e.g. reporting straight to an SVP).
	/// In that case the direct manager IS the Level 2 approver.</summary>
	private async Task<ApprovalRouting> DetermineApprovalRoutingAsync(string employeeCode, CancellationToken ct)
	{
		var manager = await employeeService.GetManagerAsync(employeeCode, ct);
		var skipManager = await employeeService.GetSkipManagerAsync(employeeCode, ct);

		if (manager is not null && skipManager is null)
			return new ApprovalRouting(SkipLevel1: true, Level1Approver: null, Level2Approver: manager);

		return new ApprovalRouting(SkipLevel1: false, Level1Approver: manager, Level2Approver: skipManager);
	}

	private async Task<Employee> RequireEmployeeAsync(string employeeCode, CancellationToken ct) =>
		await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

	private async Task EnsureIsManagerAsync(string employeeCode, string approverEmployeeCode, CancellationToken ct)
	{
		var manager = await employeeService.GetManagerAsync(employeeCode, ct);
		if (manager?.EmployeeCode != approverEmployeeCode)
			throw new BusinessRuleException($"{approverEmployeeCode} is not the Level 1 approver for {employeeCode}.");
	}

	/// <summary>Authorizes whoever DetermineApprovalRoutingAsync says is the
	/// real Level 2 approver — the manager's manager normally, or the direct
	/// manager themselves in the skip-Level-1 case.</summary>
	private async Task EnsureIsLevel2ApproverAsync(string employeeCode, string approverEmployeeCode, CancellationToken ct)
	{
		var routing = await DetermineApprovalRoutingAsync(employeeCode, ct);
		if (routing.Level2Approver?.EmployeeCode != approverEmployeeCode)
			throw new BusinessRuleException($"{approverEmployeeCode} is not the Level 2 approver for {employeeCode}.");
	}

	private static void AddEvent(WeekRecord week, string text, string? meta, ApprovalEventStatus? status, int? actedByEmployeeId) =>
		week.ApprovalEvents.Add(new ApprovalEvent
		{
			EventText = text,
			EventMeta = meta,
			EventStatus = status,
			ActedByEmployeeId = actedByEmployeeId,
			EventTimestamp = DateTime.UtcNow,
		});

	private static WeekRecordDto ToDto(string employeeCode, WeekRecord week)
	{
		var trail = week.ApprovalEvents
			.OrderBy(e => e.EventTimestamp)
			.Select(e => new ApprovalEventDto(e.EventText, e.EventMeta, e.EventStatus?.ToString(), e.EventTimestamp))
			.ToList();

		return new WeekRecordDto(
			employeeCode,
			week.WeekStartDate,
			week.Status.ToString(),
			week.SubmittedAt,
			week.RejectedBy?.FullName,
			week.RejectionReason,
			trail
		);
	}
}