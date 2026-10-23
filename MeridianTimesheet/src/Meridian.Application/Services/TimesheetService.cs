using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class TimesheetService(
	IEmployeeRepository employeeRepository,
	ITimeEntryRepository timeEntryRepository,
	IDayTypeRepository dayTypeRepository,
	IWeekRecordRepository weekRecordRepository,
	IMasterDataRepository masterDataRepository,
	IDayTypeResolutionService dayTypeResolutionService,
	IDayTypeRequestRepository dayTypeRequestRepository) : ITimesheetService
{
	public async Task<WeekSummaryDto> GetWeekAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var entries = await timeEntryRepository.GetForWeekAsync(employee.EmployeeId, weekStartDate, ct);
		var dayTypeDtos = await dayTypeResolutionService.ResolveWeekAsync(employee.EmployeeId, weekStartDate, ct);
		var weekRecord = await weekRecordRepository.GetAsync(employee.EmployeeId, weekStartDate, ct);

		var entryDtos = entries.Select(e => ToDto(e, employeeCode)).ToList();
		var total = entries.Sum(e => e.TotalHours);
		var billable = entries.Where(e => e.Classification == "Billable").Sum(e => e.TotalHours);
		var partialBillable = entries.Where(e => e.Classification == "PartialBillable").Sum(e => e.TotalHours);
		var capacity = dayTypeDtos.Sum(d => d.CapacityHours);

		var dayTypeRequests = await dayTypeRequestRepository.GetForEmployeeWeekAsync(employee.EmployeeId, weekStartDate, ct);
		var dayTypeRequestDtos = new List<DayTypeRequestDto>(dayTypeRequests.Count);
		foreach (var request in dayTypeRequests)
			dayTypeRequestDtos.Add(await ToDayTypeRequestDtoAsync(request, employee, ct));

		return new WeekSummaryDto(
			ToWeekRecordDto(employeeCode, weekStartDate, weekRecord),
			entryDtos,
			dayTypeDtos,
			total,
			billable,
			partialBillable,
			capacity,
			dayTypeRequestDtos
		);
	}

	private async Task<DayTypeRequestDto> ToDayTypeRequestDtoAsync(Domain.Entities.DayTypeRequest request, Employee employee, CancellationToken ct)
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

	public async Task<IReadOnlyList<WeekHistoryItemDto>> GetHistoryAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var weeklyTotals = await timeEntryRepository.GetWeeklyTotalsAsync(employee.EmployeeId, ct);
		var weekRecords = await weekRecordRepository.GetAllForEmployeeAsync(employee.EmployeeId, ct);
		var recordByWeek = weekRecords.ToDictionary(w => w.WeekStartDate);

		// Every week that has any logged hours, cross-referenced with its
		// WeekRecord if one exists (never-submitted weeks won't have one �
		// they're still shown, just as Draft, same convention used elsewhere).
		var fromEntries = weeklyTotals.Select(w =>
		{
			recordByWeek.TryGetValue(w.WeekStartDate, out var record);
			return new WeekHistoryItemDto(
				w.WeekStartDate,
				(record?.Status ?? WeekStatus.Draft).ToString(),
				w.TotalHours,
				w.BillableHours,
				w.PartialBillableHours,
				record?.SubmittedAt
			);
		});

		// A week could theoretically have a WeekRecord (e.g. submitted, then
		// every entry later removed) with zero remaining hours � include
		// those too so a submitted-but-now-empty week isn't silently hidden.
		var entryWeeks = weeklyTotals.Select(w => w.WeekStartDate).ToHashSet();
		var fromRecordsOnly = weekRecords
			.Where(r => !entryWeeks.Contains(r.WeekStartDate))
			.Select(r => new WeekHistoryItemDto(r.WeekStartDate, r.Status.ToString(), 0m, 0m, 0m, r.SubmittedAt));

		return fromEntries.Concat(fromRecordsOnly)
			.OrderByDescending(w => w.WeekStartDate)
			.ToList();
	}

	public async Task<TimeEntryDto> AddEntryAsync(string employeeCode, DateOnly weekStartDate, CreateTimeEntryRequest request, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		await EnsureWeekEditableAsync(employee.EmployeeId, employeeCode, weekStartDate, ct);
		if (string.IsNullOrWhiteSpace(request.Note))
			throw new BusinessRuleException("A description is required for this task line.");
		EnsureNoteLength(request.Note);
		TimeEntryRules.ValidateHours(request.HoursByDay);
		await EnsureValidHierarchyAsync(request.ProjectId, request.ModuleId, request.TaskId, requireActiveProject: true, ct);

		// Classification/BillingCategory are set by the admin at project-
		// allocation time, not chosen per line - derive them from whichever
		// allocation matches this project. The one case with no allocation to
		// derive from is a project just quick-added via "+ Others", which is
		// pending admin review and has no allocation yet - for that one case
		// only, fall back to whatever the employee picked, same as before
		// this feature.
		var allocations = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		var allocation = allocations.FirstOrDefault(a => a.ProjectId == request.ProjectId);
		var classification = allocation?.Classification ?? request.Classification;
		var billingCategory = allocation is not null ? allocation.BillingCategory : request.BillingCategory;
		BillingClassificationRules.Validate(classification, billingCategory);

		var entry = new TimeEntry
		{
			EmployeeId = employee.EmployeeId,
			WeekStartDate = weekStartDate,
			ProjectId = request.ProjectId,
			ModuleId = request.ModuleId,
			TaskId = request.TaskId,
			Classification = classification,
			BillingCategory = billingCategory,
			Note = request.Note,
			CreatedAt = DateTime.UtcNow,
		};
		entry.HoursByDay = request.HoursByDay;

		await timeEntryRepository.AddAsync(entry, ct);
		await timeEntryRepository.SaveChangesAsync(ct);
		return ToDto(entry, employeeCode);
	}

	public async Task<TimeEntryDto> UpdateEntryAsync(int timeEntryId, UpdateTimeEntryRequest request, CancellationToken ct = default)
	{
		var entry = await timeEntryRepository.GetByIdAsync(timeEntryId, ct)
			?? throw new EntityNotFoundException(nameof(TimeEntry), timeEntryId);

		var employee = await employeeRepository.GetByIdAsync(entry.EmployeeId, ct)
			?? throw new EntityNotFoundException(nameof(Employee), entry.EmployeeId);
		await EnsureWeekEditableAsync(employee.EmployeeId, employee.EmployeeCode, entry.WeekStartDate, ct);

		if (request.ProjectId is not null || request.ModuleId is not null || request.TaskId is not null)
		{
			// Only re-checked when the line is actually re-pointed - an hours-only
			// edit on a line whose project was later deactivated must still work.
			await EnsureValidHierarchyAsync(
				request.ProjectId ?? entry.ProjectId, request.ModuleId ?? entry.ModuleId, request.TaskId ?? entry.TaskId,
				requireActiveProject: request.ProjectId is int newProjectId && newProjectId != entry.ProjectId, ct);
		}
		if (request.ProjectId is int p) entry.ProjectId = p;
		if (request.ModuleId is int m) entry.ModuleId = m;
		if (request.TaskId is int t) entry.TaskId = t;

		// Classification/BillingCategory are set by the admin at project-
		// allocation time, not chosen per line (same rule AddEntryAsync
		// applies) - whenever the entry's (possibly just-changed) project has
		// an allocation, it always wins over whatever the client sent for
		// either field. The one case with no allocation to defer to is a
		// project quick-added via "+ Others" and still pending admin review -
		// only there does the client's explicit Classification/BillingCategory
		// (still chosen manually in EntryDrawer for that one case) apply.
		var allocations = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		var allocation = allocations.FirstOrDefault(a => a.ProjectId == entry.ProjectId);
		if (allocation is not null)
		{
			entry.Classification = allocation.Classification;
			entry.BillingCategory = allocation.BillingCategory;
		}
		else
		{
			if (request.Classification is string cls) entry.Classification = cls;
			if (request.BillingCategory is not null)
			{
				entry.BillingCategory = request.BillingCategory;
			}
			else if (entry.BillingCategory is not null
				&& BillingClassificationRules.AllowedCategories.TryGetValue(entry.Classification, out var allowedCategories)
				&& !allowedCategories.Contains(entry.BillingCategory))
			{
				// Classification changed (e.g. to PartialBillable, which allows no category at
				// all) and left a now-incompatible category behind. UpdateTimeEntryRequest can't
				// explicitly clear BillingCategory back to null, so if the caller didn't set a new
				// one, drop the stale value here rather than blocking the save on a field the user
				// never touched.
				entry.BillingCategory = null;
			}
		}
		if (request.Note is not null)
		{
			// Reject an explicit attempt to clear the description to blank, but don't
			// require Note on updates that never touch it (e.g. an hours-only edit typed
			// directly into the week grid) - otherwise a pre-existing line with no
			// description (like one carried forward from last week) couldn't have its
			// hours edited until someone opens it in the drawer, which is the one place
			// a blank Note truly can't be saved (see AddEntryAsync / EntryDrawer.tsx).
			if (string.IsNullOrWhiteSpace(request.Note))
				throw new BusinessRuleException("A description is required for this task line.");
			EnsureNoteLength(request.Note);
			entry.Note = request.Note;
		}
		if (request.HoursByDay is decimal[] hours)
		{
			TimeEntryRules.ValidateHours(hours);
			entry.HoursByDay = hours;
		}
		entry.UpdatedAt = DateTime.UtcNow;
		BillingClassificationRules.Validate(entry.Classification, entry.BillingCategory);

		await timeEntryRepository.SaveChangesAsync(ct);
		return ToDto(entry, employee.EmployeeCode);
	}

	public async Task RemoveEntryAsync(int timeEntryId, CancellationToken ct = default)
	{
		var entry = await timeEntryRepository.GetByIdAsync(timeEntryId, ct)
			?? throw new EntityNotFoundException(nameof(TimeEntry), timeEntryId);

		var employee = await employeeRepository.GetByIdAsync(entry.EmployeeId, ct)
			?? throw new EntityNotFoundException(nameof(Employee), entry.EmployeeId);
		await EnsureWeekEditableAsync(employee.EmployeeId, employee.EmployeeCode, entry.WeekStartDate, ct);

		timeEntryRepository.Remove(entry);
		await timeEntryRepository.SaveChangesAsync(ct);
	}

	public async Task<string?> GetEntryOwnerCodeAsync(int timeEntryId, CancellationToken ct = default)
	{
		var entry = await timeEntryRepository.GetByIdAsync(timeEntryId, ct);
		if (entry is null) return null;
		var employee = await employeeRepository.GetByIdAsync(entry.EmployeeId, ct);
		return employee?.EmployeeCode;
	}

	public async Task<DayTypeDto> SetDayTypeAsync(string employeeCode, DateOnly date, string dayType, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var weekStart = WeekMath.MondayOf(date);
		await EnsureWeekEditableAsync(employee.EmployeeId, employeeCode, weekStart, ct);

		if (!Enum.TryParse<DayType>(dayType, out var parsed) || parsed is not (DayType.W or DayType.WFH))
			throw new BusinessRuleException(
				"Only 'W' (working) or 'WFH' can be set directly. Holiday and Leave are synced from the holiday " +
				"calendar and KEKA respectively and can't be edited here.");

		var holiday = await masterDataRepository.GetHolidayOnAsync(date, employee.PrimaryAccountId, ct);
		if (holiday is not null)
			throw new BusinessRuleException($"{date:d MMM} is a holiday ({holiday.Name}) and can't be changed here.");

		await dayTypeRepository.SetAsync(employee.EmployeeId, date, parsed, ct);
		await dayTypeRepository.SaveChangesAsync(ct);

		var capacity = parsed is DayType.W or DayType.WFH ? WeekMath.StandardHoursPerDay : 0m;
		return new DayTypeDto(date, parsed.ToString(), capacity);
	}

	public async Task<int> CopyLastWeekAsync(string employeeCode, DateOnly weekStartDate, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		await EnsureWeekEditableAsync(employee.EmployeeId, employeeCode, weekStartDate, ct);

		var previousWeek = weekStartDate.AddDays(-7);
		var previousEntries = await timeEntryRepository.GetForWeekAsync(employee.EmployeeId, previousWeek, ct);
		var currentEntries = await timeEntryRepository.GetForWeekAsync(employee.EmployeeId, weekStartDate, ct);
		var existingKeys = currentEntries.Select(e => (e.ModuleId, e.TaskId)).ToHashSet();

		var added = 0;
		foreach (var prev in previousEntries)
		{
			if (existingKeys.Contains((prev.ModuleId, prev.TaskId))) continue;

			var copy = new TimeEntry
			{
				EmployeeId = employee.EmployeeId,
				WeekStartDate = weekStartDate,
				ProjectId = prev.ProjectId,
				ModuleId = prev.ModuleId,
				TaskId = prev.TaskId,
				Classification = prev.Classification,
				BillingCategory = prev.BillingCategory,
				Note = null,
				CreatedAt = DateTime.UtcNow,
			};
			copy.HoursByDay = [0, 0, 0, 0, 0, 0, 0];
			await timeEntryRepository.AddAsync(copy, ct);
			added++;
		}

		if (added > 0) await timeEntryRepository.SaveChangesAsync(ct);
		return added;
	}

	// ---- Shared helpers ----

	private async Task<Employee> RequireEmployeeAsync(string employeeCode, CancellationToken ct) =>
		await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

	/// <summary>The UI only ever offers a Module under the chosen Project and a
	/// Task under the chosen Module, but nothing server-side enforced that - a
	/// mismatched combination (or a hand-crafted request) would save a line
	/// whose Project, Module and Task disagree, which then rolls up wrongly in
	/// Reports and the approval detail. New lines also can't target an
	/// inactive project (the UI already hides those from Add Task Line).</summary>
	private async Task EnsureValidHierarchyAsync(int projectId, int moduleId, int taskId, bool requireActiveProject, CancellationToken ct)
	{
		var project = await masterDataRepository.GetProjectByIdAsync(projectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), projectId);
		var module = await masterDataRepository.GetModuleByIdAsync(moduleId, ct)
			?? throw new EntityNotFoundException(nameof(Module), moduleId);
		var task = await masterDataRepository.GetTaskByIdAsync(taskId, ct)
			?? throw new EntityNotFoundException(nameof(WorkTask), taskId);

		if (module.ProjectId != project.ProjectId)
			throw new BusinessRuleException($"Module \"{module.Name}\" doesn't belong to project \"{project.Name}\".");
		if (task.ModuleId != module.ModuleId)
			throw new BusinessRuleException($"Task \"{task.Name}\" doesn't belong to module \"{module.Name}\".");
		if (requireActiveProject && !project.IsActive)
			throw new BusinessRuleException($"Project \"{project.Name}\" is inactive - time can't be logged against it.");
	}

	/// <summary>Carbynetech_TimeEntry.Note is NVARCHAR(500) - over that used to
	/// surface as a generic 500 ("An unexpected error occurred").</summary>
	private static void EnsureNoteLength(string note)
	{
		if (note.Length > 500)
			throw new BusinessRuleException($"The description is {note.Length} characters - please keep it to 500 or fewer.");
	}

	private async Task EnsureWeekEditableAsync(int employeeId, string employeeCode, DateOnly weekStartDate, CancellationToken ct)
	{
		var week = await weekRecordRepository.GetAsync(employeeId, weekStartDate, ct);
		var status = week?.Status ?? WeekStatus.Draft;
		if (status is not (WeekStatus.Draft or WeekStatus.Rejected))
			throw new WeekLockedException(employeeCode, weekStartDate, status.ToString());
	}

	private static TimeEntryDto ToDto(TimeEntry entry, string employeeCode) => new(
		entry.TimeEntryId,
		employeeCode,
		entry.WeekStartDate,
		entry.ProjectId,
		entry.ModuleId,
		entry.TaskId,
		entry.Classification,
		entry.BillingCategory,
		entry.Note,
		entry.HoursByDay
	);

	private static WeekRecordDto ToWeekRecordDto(string employeeCode, DateOnly weekStartDate, WeekRecord? week)
	{
		if (week is null)
			return new WeekRecordDto(employeeCode, weekStartDate, WeekStatus.Draft.ToString(), null, null, null, []);

		var trail = week.ApprovalEvents
			.OrderBy(e => e.EventTimestamp)
			.Select(e => new ApprovalEventDto(e.EventText, e.EventMeta, e.EventStatus?.ToString(), e.EventTimestamp))
			.ToList();

		return new WeekRecordDto(
			employeeCode,
			weekStartDate,
			week.Status.ToString(),
			week.SubmittedAt,
			week.RejectedBy?.FullName,
			week.RejectionReason,
			trail
		);
	}
}