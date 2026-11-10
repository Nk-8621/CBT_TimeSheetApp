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
	IDayTypeRequestRepository dayTypeRequestRepository,
	IUserStoryRepository userStoryRepository) : ITimesheetService
{
	/// <summary>Module and Task name used for every task added under a user
	/// story. The employee only types a task name, but each timesheet line
	/// still needs a Module and Task (reports and Excel use them), so each
	/// project gets one "General › General" pair, created on first use.</summary>
	private const string GeneralName = "General";

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
		return await AddEntryCoreAsync(employee, employeeCode, weekStartDate, request, userStory: null, ct);
	}

	public async Task<TimeEntryDto> AddStoryTaskAsync(string employeeCode, DateOnly weekStartDate, CreateStoryTaskRequest request, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		await EnsureWeekEditableAsync(employee.EmployeeId, employeeCode, weekStartDate, ct);

		var story = await RequireOwnActiveStoryAsync(request.UserStoryId, employee.EmployeeId, ct);
		var taskName = request.TaskName?.Trim() ?? "";
		if (taskName.Length == 0)
			throw new BusinessRuleException("Enter the task name.");
		TimeEntryRules.ValidateHours(request.HoursByDay);
		if (request.HoursByDay.Sum() == 0)
			throw new BusinessRuleException("Enter hours for at least one day.");
		EnsureWithinStoryDates(story, weekStartDate, new decimal[7], request.HoursByDay);

		var (moduleId, taskId) = await GetGeneralModuleAndTaskAsync(story.ProjectId, ct);
		// Used only when the employee has no allocation on this project (e.g. his
		// department's "Internal Work" project) - an allocation always wins.
		var defaultClassification = story.Project?.DefaultBillable ?? "Billable";
		var defaultCategory = defaultClassification == "NonBillable" ? "OH" : null;
		var entryRequest = new CreateTimeEntryRequest(story.ProjectId, moduleId, taskId, defaultClassification, defaultCategory, taskName, request.HoursByDay);
		return await AddEntryCoreAsync(employee, employeeCode, weekStartDate, entryRequest, story, ct);
	}

	/// <summary>Shared by the classic Add Task Line and the user story task -
	/// every rule (week editable, description, 8 h per line, day limits,
	/// valid hierarchy, billing from allocation) lives here once.</summary>
	private async Task<TimeEntryDto> AddEntryCoreAsync(Employee employee, string employeeCode, DateOnly weekStartDate, CreateTimeEntryRequest request, UserStory? userStory, CancellationToken ct)
	{
		await EnsureWeekEditableAsync(employee.EmployeeId, employeeCode, weekStartDate, ct);
		if (string.IsNullOrWhiteSpace(request.Note))
			throw new BusinessRuleException("A description is required for this task line.");
		EnsureNoteLength(request.Note);
		TimeEntryRules.ValidateHours(request.HoursByDay);
		await EnsureDayLimitsAsync(employee.EmployeeId, weekStartDate, null, new decimal[7], request.HoursByDay, ct);
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
			UserStoryId = userStory?.UserStoryId,
			UserStory = userStory,
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

		var storyChanged = false;
		if (request.UserStoryId is int newStoryId && newStoryId != entry.UserStoryId)
		{
			storyChanged = true;
			// Moving a task to another of the employee's stories. If that story is
			// on a different project, the line follows it to that project's
			// General › General module and task.
			var story = await RequireOwnActiveStoryAsync(newStoryId, employee.EmployeeId, ct);
			if (story.ProjectId != entry.ProjectId)
			{
				var (generalModuleId, generalTaskId) = await GetGeneralModuleAndTaskAsync(story.ProjectId, ct);
				await EnsureValidHierarchyAsync(story.ProjectId, generalModuleId, generalTaskId, requireActiveProject: true, ct);
				entry.ProjectId = story.ProjectId;
				entry.ModuleId = generalModuleId;
				entry.TaskId = generalTaskId;
			}
			entry.UserStoryId = story.UserStoryId;
			entry.UserStory = story;
		}

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
			await EnsureDayLimitsAsync(employee.EmployeeId, entry.WeekStartDate, entry.TimeEntryId, entry.HoursByDay, hours, ct);
			if (entry.UserStory is not null)
				EnsureWithinStoryDates(entry.UserStory, entry.WeekStartDate, storyChanged ? new decimal[7] : entry.HoursByDay, hours);
			entry.HoursByDay = hours;
		}
		else if (storyChanged && entry.UserStory is not null)
		{
			// Moved to another story without touching hours: its existing hours
			// must still fall inside the new story's dates.
			EnsureWithinStoryDates(entry.UserStory, entry.WeekStartDate, new decimal[7], entry.HoursByDay);
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
		var existingKeys = currentEntries.Select(CopyKey).ToHashSet();

		var added = 0;
		foreach (var prev in previousEntries)
		{
			if (!existingKeys.Add(CopyKey(prev))) continue;
			if (prev.UserStory is { IsActive: false }) continue;
			// A story that doesn't run in the new week isn't carried forward.
			if (prev.UserStory is UserStory s && (s.EndDate < weekStartDate || s.StartDate > weekStartDate.AddDays(6))) continue;

			var copy = new TimeEntry
			{
				EmployeeId = employee.EmployeeId,
				WeekStartDate = weekStartDate,
				ProjectId = prev.ProjectId,
				ModuleId = prev.ModuleId,
				TaskId = prev.TaskId,
				Classification = prev.Classification,
				BillingCategory = prev.BillingCategory,
				// A story task's Note is its name, so it comes along; a classic
				// line's description is week-specific and starts blank as before.
				Note = prev.UserStoryId is null ? null : prev.Note,
				UserStoryId = prev.UserStoryId,
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

	/// <summary>A day can never hold more than its available hours (8 h, 4 h on
	/// a half-day leave, 0 on leave / holiday / weekly off) across all lines.
	/// Only days where this line's hours go UP are checked, so reducing or
	/// moving hours on an existing line is always allowed.</summary>
	private async Task EnsureDayLimitsAsync(int employeeId, DateOnly weekStartDate, int? excludeEntryId, decimal[] oldHours, decimal[] newHours, CancellationToken ct)
	{
		var entries = await timeEntryRepository.GetForWeekAsync(employeeId, weekStartDate, ct);
		var dayTypes = await dayTypeResolutionService.ResolveWeekAsync(employeeId, weekStartDate, ct);

		for (var i = 0; i < 7; i++)
		{
			if (newHours[i] <= oldHours[i]) continue;

			var otherLinesTotal = entries
				.Where(e => e.TimeEntryId != excludeEntryId)
				.Sum(e => e.HoursByDay[i]);
			var capacity = dayTypes[i].CapacityHours;
			if (otherLinesTotal + newHours[i] <= capacity) continue;

			var left = Math.Max(0m, capacity - otherLinesTotal);
			var label = dayTypes[i].Date.ToString("ddd d MMM");
			throw new BusinessRuleException(left == 0
				? $"{label} is already full ({capacity:0.##} h). Edit an existing line instead of adding more hours."
				: $"{label} has only {left:0.##} h left (a day can hold at most {capacity:0.##} h).");
		}
	}

	/// <summary>Story tasks are matched by story + task name; classic lines by
	/// module + task, as before.</summary>
	private static string CopyKey(TimeEntry e) =>
		e.UserStoryId is int storyId
			? $"story:{storyId}:{(e.Note ?? "").Trim().ToLowerInvariant()}"
			: $"line:{e.ModuleId}:{e.TaskId}";

	/// <summary>A user story task can only get hours on days inside the story's
	/// start-end dates. Only days where hours go UP are checked, so reducing or
	/// clearing hours already logged outside the dates (e.g. after the story's
	/// dates were changed) is always allowed.</summary>
	private static void EnsureWithinStoryDates(UserStory story, DateOnly weekStartDate, decimal[] oldHours, decimal[] newHours)
	{
		for (var i = 0; i < 7; i++)
		{
			if (newHours[i] <= oldHours[i]) continue;

			var date = weekStartDate.AddDays(i);
			if (date < story.StartDate || date > story.EndDate)
				throw new BusinessRuleException(
					$"{date:ddd d MMM} is outside the dates of user story \"{story.Name}\" ({story.StartDate:d MMM} - {story.EndDate:d MMM yyyy}). Change the story's dates to log hours on that day.");
		}
	}

	private async Task<UserStory> RequireOwnActiveStoryAsync(int userStoryId, int employeeId, CancellationToken ct)
	{
		var story = await userStoryRepository.GetByIdAsync(userStoryId, ct)
			?? throw new EntityNotFoundException(nameof(UserStory), userStoryId);
		if (story.EmployeeId != employeeId)
			throw new BusinessRuleException("This user story belongs to another employee.");
		if (!story.IsActive)
			throw new BusinessRuleException($"User story \"{story.Name}\" is closed - tasks can't be added to it.");
		return story;
	}

	/// <summary>Finds the project's "General" module and its "General" task,
	/// creating either one the first time it's needed. Reuses an existing
	/// module/task of that name instead of adding a duplicate.</summary>
	private async Task<(int ModuleId, int TaskId)> GetGeneralModuleAndTaskAsync(int projectId, CancellationToken ct)
	{
		var module = (await masterDataRepository.GetModulesAsync(projectId, ct))
			.FirstOrDefault(m => string.Equals(m.Name, GeneralName, StringComparison.OrdinalIgnoreCase));
		if (module is null)
		{
			module = new Module { ProjectId = projectId, Name = GeneralName, CreatedAt = DateTime.UtcNow };
			await masterDataRepository.AddModuleAsync(module, ct);
			await masterDataRepository.SaveChangesAsync(ct);
		}

		var task = (await masterDataRepository.GetTasksAsync(module.ModuleId, ct))
			.FirstOrDefault(t => string.Equals(t.Name, GeneralName, StringComparison.OrdinalIgnoreCase));
		if (task is null)
		{
			task = new WorkTask { ModuleId = module.ModuleId, Name = GeneralName, CreatedAt = DateTime.UtcNow };
			await masterDataRepository.AddTaskAsync(task, ct);
			await masterDataRepository.SaveChangesAsync(ct);
		}

		return (module.ModuleId, task.TaskId);
	}

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
		entry.HoursByDay,
		entry.UserStoryId,
		entry.UserStory?.Name,
		entry.UserStory?.StartDate,
		entry.UserStory?.EndDate
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