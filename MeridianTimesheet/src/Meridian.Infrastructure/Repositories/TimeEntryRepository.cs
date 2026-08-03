using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class TimeEntryRepository(MeridianDbContext db) : ITimeEntryRepository
{
	public async Task<IReadOnlyList<TimeEntry>> GetForWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default) =>
		await db.TimeEntries
			.Include(t => t.Project)
			.Include(t => t.Module)
			.Include(t => t.Task)
			.Where(t => t.EmployeeId == employeeId && t.WeekStartDate == weekStartDate)
			.ToListAsync(ct);

	public Task<TimeEntry?> GetByIdAsync(int timeEntryId, CancellationToken ct = default) =>
		db.TimeEntries
			.Include(t => t.Task)
			.FirstOrDefaultAsync(t => t.TimeEntryId == timeEntryId, ct);

	public async Task AddAsync(TimeEntry entry, CancellationToken ct = default) =>
		await db.TimeEntries.AddAsync(entry, ct);

	public void Remove(TimeEntry entry) => db.TimeEntries.Remove(entry);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

	public async Task<IReadOnlyList<WeeklyHoursAggregate>> GetWeeklyTotalsAsync(int employeeId, CancellationToken ct = default)
	{
		var raw = await db.TimeEntries
			.Where(t => t.EmployeeId == employeeId)
			.GroupBy(t => t.WeekStartDate)
			.Select(g => new
			{
				WeekStartDate = g.Key,
				Total = g.Sum(t => t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours),
				Billable = g.Where(t => t.IsBillable).Sum(t => t.MondayHours + t.TuesdayHours + t.WednesdayHours + t.ThursdayHours + t.FridayHours + t.SaturdayHours + t.SundayHours),
			})
			.OrderByDescending(g => g.WeekStartDate)
			.ToListAsync(ct);

		return raw.Select(r => new WeeklyHoursAggregate(r.WeekStartDate, r.Total, r.Billable)).ToList();
	}
}