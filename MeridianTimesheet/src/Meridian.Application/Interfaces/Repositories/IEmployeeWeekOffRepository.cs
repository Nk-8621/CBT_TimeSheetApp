using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

/// <summary>WEEK OFFS (Nov 2026): employee-specific week offs (Master Data -> Week Offs).</summary>
public interface IEmployeeWeekOffRepository
{
	/// <summary>Every record, with Employee, Account and CreatedBy loaded - for the Week Offs list.</summary>
	Task<IReadOnlyList<EmployeeWeekOff>> GetAllAsync(CancellationToken ct = default);

	/// <summary>One employee's records that overlap the given dates.</summary>
	Task<IReadOnlyList<EmployeeWeekOff>> GetForEmployeeAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);

	/// <summary>All of one employee's records (used for the overlap check).</summary>
	Task<IReadOnlyList<EmployeeWeekOff>> GetAllForEmployeeAsync(int employeeId, CancellationToken ct = default);

	Task<EmployeeWeekOff?> GetByIdAsync(int employeeWeekOffId, CancellationToken ct = default);
	Task AddAsync(EmployeeWeekOff weekOff, CancellationToken ct = default);
	void Remove(EmployeeWeekOff weekOff);
	Task SaveChangesAsync(CancellationToken ct = default);
}
