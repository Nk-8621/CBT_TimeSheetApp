using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class DayTypeRequestService(
	IEmployeeRepository employeeRepository,
	ITimeEntryRepository timeEntryRepository,
	IDayTypeRepository dayTypeRepository,
	IDayTypeRequestRepository dayTypeRequestRepository,
	IWeekRecordRepository weekRecordRepository,
	IMasterDataRepository masterDataRepository,
	IEmployeeService employeeService) : IDayTypeRequestService
{
	public async Task<DayTypeRequestDto> SubmitAsync(string employeeCode, DateOnly date, string requestType, string? note, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);

		if (!Enum.TryParse<DayTypeRequestType>(requestType, out var type))
			throw new BusinessRuleException("Request type must be one of WFH, LeaveFirstHalf, LeaveSecondHalf or LeaveFull.");
		if (string.IsNullOrWhiteSpace(note))
			throw new BusinessRuleException("A note is required for this request.");
		if (date < DateOnly.FromDateTime(DateTime.UtcNow))
			throw new BusinessRuleException("WFH/Leave can only be requested for today or a future date, not a day that's already passed.");

		await EnsureWeekEditableAsync(employee.EmployeeId, employeeCode, WeekMath.MondayOf(date), ct);

		var holiday = await masterDataRepository.GetHolidayOnAsync(date, employee.PrimaryAccountId, ct);
		if (holiday is not null)
			throw new BusinessRuleException($"{date:d MMM} is already a holiday ({holiday.Name}) - nothing to request.");
		if (WeekMath.IsWeekend(date))
			throw new BusinessRuleException($"{date:d MMM} is a weekly off day - nothing to request.");

		var existing = await dayTypeRequestRepository.GetActiveForDateAsync(employee.EmployeeId, date, ct);
		if (existing is not null)
			throw new BusinessRuleException($"There's already a {existing.Status.ToString().ToLowerInvariant()} {existing.RequestType} request for {date:d MMM}.");

		// The requested effect takes hold right away (shown as Pending until a
		// manager acts on it) - so before applying it, make sure it wouldn't
		// silently strand hours the employee already logged that day. If it
		// would, they need to clear/reduce those hours first rather than have
		// this quietly leave the day over capacity.
		var newCapacity = CapacityFor(type);
		var entries = await timeEntryRepository.GetForWeekAsync(employee.EmployeeId, WeekMath.MondayOf(date), ct);
		var dayIndex = Array.IndexOf(WeekMath.WeekDays(WeekMath.MondayOf(date)), date);
		var loggedHours = entries.Sum(e => e.HoursByDay[dayIndex]);
		if (loggedHours > newCapacity)
			throw new BusinessRuleException(
				$"{date:d MMM} already has {loggedHours:0.##}h logged, which is more than the {newCapacity:0.##}h this request would leave available. " +
				"Remove or reduce those hours first, then submit the request.");

		var request = new DayTypeRequest
		{
			EmployeeId = employee.EmployeeId,
			RequestDate = date,
			RequestType = type,
			Status = DayTypeRequestStatus.Pending,
			Note = note,
			SubmittedAt = DateTime.UtcNow,
		};
		await dayTypeRequestRepository.AddAsync(request, ct);

		if (type is DayTypeRequestType.WFH)
			await dayTypeRepository.SetAsync(employee.EmployeeId, date, DayType.WFH, ct);
		// LeaveFirstHalf/LeaveSecondHalf/LeaveFull need no separate write - DayTypeResolutionService
		// picks up any Pending-or-Approved leave request directly.

		await dayTypeRequestRepository.SaveChangesAsync(ct);
		await dayTypeRepository.SaveChangesAsync(ct);

		return await ToDtoAsync(request, employee, ct);
	}

	public async Task<DayTypeRequestDto> ApproveAsync(int dayTypeRequestId, string approverEmployeeCode, CancellationToken ct = default)
	{
		var request = await RequireRequestAsync(dayTypeRequestId, ct);
		if (request.Status is not DayTypeRequestStatus.Pending)
			throw new BusinessRuleException("This request has already been decided.");

		var employee = await RequireEmployeeByIdAsync(request.EmployeeId, ct);
		await EnsureIsManagerAsync(employee.EmployeeCode, approverEmployeeCode, ct);
		var approver = await RequireEmployeeAsync(approverEmployeeCode, ct);

		// The effect was already applied at submission time - approval just
		// confirms it; nothing else to change on the day type or hours.
		request.Status = DayTypeRequestStatus.Approved;
		request.ApproverEmployeeId = approver.EmployeeId;
		request.DecidedAt = DateTime.UtcNow;

		await dayTypeRequestRepository.SaveChangesAsync(ct);
		return await ToDtoAsync(request, employee, ct);
	}

	public async Task<DayTypeRequestDto> RejectAsync(int dayTypeRequestId, string approverEmployeeCode, string? comment, CancellationToken ct = default)
	{
		var request = await RequireRequestAsync(dayTypeRequestId, ct);
		if (request.Status is not DayTypeRequestStatus.Pending)
			throw new BusinessRuleException("This request has already been decided.");

		var employee = await RequireEmployeeByIdAsync(request.EmployeeId, ct);
		await EnsureIsManagerAsync(employee.EmployeeCode, approverEmployeeCode, ct);
		var approver = await RequireEmployeeAsync(approverEmployeeCode, ct);

		// Reverse whatever was applied at submission. WFH used the existing
		// override table, so clear it back to a plain working day; a leave
		// request needs nothing undone here - flipping its Status away from
		// Pending/Approved already excludes it from DayTypeResolutionService's
		// "active leave" query, so the day resolves back on its own.
		if (request.RequestType is DayTypeRequestType.WFH)
			await dayTypeRepository.SetAsync(employee.EmployeeId, request.RequestDate, DayType.W, ct);

		request.Status = DayTypeRequestStatus.Rejected;
		request.ApproverEmployeeId = approver.EmployeeId;
		request.DecidedAt = DateTime.UtcNow;
		request.DecisionComment = comment;

		await dayTypeRequestRepository.SaveChangesAsync(ct);
		await dayTypeRepository.SaveChangesAsync(ct);
		return await ToDtoAsync(request, employee, ct);
	}

	public async Task<IReadOnlyList<DayTypeRequestDto>> GetMyRequestsAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var requests = await dayTypeRequestRepository.GetForEmployeeAsync(employee.EmployeeId, ct);
		var result = new List<DayTypeRequestDto>(requests.Count);
		foreach (var request in requests) result.Add(await ToDtoAsync(request, employee, ct));
		return result;
	}

	public async Task<IReadOnlyList<DayTypeRequestDto>> GetApprovalQueueAsync(string approverEmployeeCode, CancellationToken ct = default)
	{
		var pending = await dayTypeRequestRepository.GetPendingAsync(ct);
		var result = new List<DayTypeRequestDto>();
		foreach (var request in pending)
		{
			var employee = await employeeRepository.GetByIdAsync(request.EmployeeId, ct);
			if (employee is null) continue;

			var manager = await employeeService.GetManagerAsync(employee.EmployeeCode, ct);
			if (manager?.EmployeeCode != approverEmployeeCode) continue;

			result.Add(await ToDtoAsync(request, employee, ct));
		}
		return result;
	}

	private static decimal CapacityFor(DayTypeRequestType type) => type switch
	{
		DayTypeRequestType.WFH => WeekMath.StandardHoursPerDay,
		DayTypeRequestType.LeaveFirstHalf or DayTypeRequestType.LeaveSecondHalf => WeekMath.StandardHoursPerDay / 2,
		DayTypeRequestType.LeaveFull => 0m,
		_ => 0m,
	};

	private async Task EnsureWeekEditableAsync(int employeeId, string employeeCode, DateOnly weekStartDate, CancellationToken ct)
	{
		var week = await weekRecordRepository.GetAsync(employeeId, weekStartDate, ct);
		var status = week?.Status ?? WeekStatus.Draft;
		if (status is not (WeekStatus.Draft or WeekStatus.Rejected))
			throw new WeekLockedException(employeeCode, weekStartDate, status.ToString());
	}

	private async Task EnsureIsManagerAsync(string employeeCode, string approverEmployeeCode, CancellationToken ct)
	{
		var manager = await employeeService.GetManagerAsync(employeeCode, ct);
		if (manager?.EmployeeCode != approverEmployeeCode)
			throw new BusinessRuleException($"{approverEmployeeCode} is not the Level 1 approver for {employeeCode}.");
	}

	private async Task<Employee> RequireEmployeeAsync(string employeeCode, CancellationToken ct) =>
		await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

	private async Task<Employee> RequireEmployeeByIdAsync(int employeeId, CancellationToken ct) =>
		await employeeRepository.GetByIdAsync(employeeId, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeId);

	private async Task<DayTypeRequest> RequireRequestAsync(int dayTypeRequestId, CancellationToken ct) =>
		await dayTypeRequestRepository.GetByIdAsync(dayTypeRequestId, ct)
			?? throw new EntityNotFoundException(nameof(DayTypeRequest), dayTypeRequestId);

	private async Task<DayTypeRequestDto> ToDtoAsync(DayTypeRequest request, Employee employee, CancellationToken ct)
	{
		string? approverName = null;
		if (request.ApproverEmployeeId is int approverId)
		{
			var approver = await employeeRepository.GetByIdAsync(approverId, ct);
			approverName = approver?.FullName;
		}

		return new DayTypeRequestDto(
			request.DayTypeRequestId,
			employee.EmployeeCode,
			employee.FullName,
			request.RequestDate,
			request.RequestType.ToString(),
			request.Status.ToString(),
			request.Note,
			request.SubmittedAt,
			approverName,
			request.DecidedAt,
			request.DecisionComment
		);
	}
}
