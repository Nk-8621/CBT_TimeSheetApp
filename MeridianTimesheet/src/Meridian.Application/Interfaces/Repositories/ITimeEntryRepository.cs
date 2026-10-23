using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

public record WeeklyHoursAggregate(DateOnly WeekStartDate, decimal TotalHours, decimal BillableHours, decimal PartialBillableHours);

public interface ITimeEntryRepository
{
	Task<IReadOnlyList<TimeEntry>> GetForWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);
	Task<TimeEntry?> GetByIdAsync(int timeEntryId, CancellationToken ct = default);
	Task AddAsync(TimeEntry entry, CancellationToken ct = default);
	void Remove(TimeEntry entry);
	Task SaveChangesAsync(CancellationToken ct = default);

	/// <summary>Every week this employee has logged any hours in, newest first.
	/// NOTE: fetches across all history with no pagination � fine at MVP scale,
	/// worth revisiting if this needs to scale to years of data.</summary>
	Task<IReadOnlyList<WeeklyHoursAggregate>> GetWeeklyTotalsAsync(int employeeId, CancellationToken ct = default);

	/// <summary>How many timesheet lines (any employee, any week) point at this
	/// Module / Task - decides whether it can still be deleted, and whether a
	/// rename would change names on existing timesheets.</summary>
	Task<int> CountForModuleAsync(int moduleId, CancellationToken ct = default);
	Task<int> CountForTaskAsync(int taskId, CancellationToken ct = default);

	/// <summary>Every line for these employees across the weeks from..to
	/// (inclusive, by WeekStartDate) in ONE query - Reports and Team Compliance
	/// use this instead of one query per employee per week.</summary>
	Task<IReadOnlyList<TimeEntry>> GetForEmployeesAsync(IReadOnlyList<int> employeeIds, DateOnly fromWeek, DateOnly toWeek, CancellationToken ct = default);
}
