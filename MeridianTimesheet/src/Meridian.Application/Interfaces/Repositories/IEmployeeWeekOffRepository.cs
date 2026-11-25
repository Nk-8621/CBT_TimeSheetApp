using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

/// <summary>WEEK OFFS (Nov 2026): client week offs and the employees assigned to them.</summary>
public interface IEmployeeWeekOffRepository
{
	// ---- Client week offs ----

	/// <summary>Every client week off, with Account loaded - for the Week Offs list.</summary>
	Task<IReadOnlyList<ClientWeekOff>> GetClientWeekOffsAsync(CancellationToken ct = default);

	/// <summary>All week-off periods of the given clients.</summary>
	Task<IReadOnlyList<ClientWeekOff>> GetClientWeekOffsForAccountsAsync(IReadOnlyCollection<int> accountIds, CancellationToken ct = default);

	Task<ClientWeekOff?> GetClientWeekOffByIdAsync(int clientWeekOffId, CancellationToken ct = default);
	Task AddClientWeekOffAsync(ClientWeekOff weekOff, CancellationToken ct = default);
	void RemoveClientWeekOff(ClientWeekOff weekOff);

	// ---- Employee assignments ----

	/// <summary>Every employee assignment, with Employee and CreatedBy loaded.</summary>
	Task<IReadOnlyList<EmployeeWeekOff>> GetAllAsync(CancellationToken ct = default);

	/// <summary>One employee's assignments that overlap the given dates.</summary>
	Task<IReadOnlyList<EmployeeWeekOff>> GetForEmployeeAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);

	/// <summary>All of one employee's assignments (used for the overlap check).</summary>
	Task<IReadOnlyList<EmployeeWeekOff>> GetAllForEmployeeAsync(int employeeId, CancellationToken ct = default);

	/// <summary>How many employee assignments a client has.</summary>
	Task<int> CountForAccountAsync(int accountId, CancellationToken ct = default);

	Task<EmployeeWeekOff?> GetByIdAsync(int employeeWeekOffId, CancellationToken ct = default);
	Task AddAsync(EmployeeWeekOff weekOff, CancellationToken ct = default);
	void Remove(EmployeeWeekOff weekOff);

	Task SaveChangesAsync(CancellationToken ct = default);
}
