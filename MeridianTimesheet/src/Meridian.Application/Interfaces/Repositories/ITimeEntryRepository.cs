using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

public record WeeklyHoursAggregate(DateOnly WeekStartDate, decimal TotalHours, decimal BillableHours);

public interface ITimeEntryRepository
{
	Task<IReadOnlyList<TimeEntry>> GetForWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);
	Task<TimeEntry?> GetByIdAsync(int timeEntryId, CancellationToken ct = default);
	Task AddAsync(TimeEntry entry, CancellationToken ct = default);
	void Remove(TimeEntry entry);
	Task SaveChangesAsync(CancellationToken ct = default);

	/// <summary>Every week this employee has logged any hours in, newest first.
	/// NOTE: fetches across all history with no pagination — fine at MVP scale,
	/// worth revisiting if this needs to scale to years of data.</summary>
	Task<IReadOnlyList<WeeklyHoursAggregate>> GetWeeklyTotalsAsync(int employeeId, CancellationToken ct = default);
}
