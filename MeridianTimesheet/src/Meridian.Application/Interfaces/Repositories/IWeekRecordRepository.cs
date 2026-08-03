using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

public interface IWeekRecordRepository
{
	/// <summary>Gets the week record for this employee/week, or null if they've never
	/// touched it (which the service layer treats the same as a fresh Draft).</summary>
	Task<WeekRecord?> GetAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);
	Task<WeekRecord> GetOrCreateAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);
	Task<IReadOnlyList<WeekRecord>> GetByStatusAsync(Domain.Enums.WeekStatus status, CancellationToken ct = default);

	/// <summary>All week records this employee has ever submitted, for the history
	/// screen. Weeks that were only ever a draft (never submitted) won't have a
	/// row here — the service layer combines this with time-entry data to cover
	/// those too.</summary>
	Task<IReadOnlyList<WeekRecord>> GetAllForEmployeeAsync(int employeeId, CancellationToken ct = default);

	Task SaveChangesAsync(CancellationToken ct = default);
}
