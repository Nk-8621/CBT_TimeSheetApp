using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

/// <summary>WEEK OFFS (Nov 2026): see IEmployeeWeekOffRepository.</summary>
public class EmployeeWeekOffRepository(MeridianDbContext db) : IEmployeeWeekOffRepository
{
	// ---- Client week offs ----

	public async Task<IReadOnlyList<ClientWeekOff>> GetClientWeekOffsAsync(CancellationToken ct = default) =>
		await db.ClientWeekOffs
			.AsNoTracking()
			.Include(w => w.Account)
			.OrderBy(w => w.Account!.Name)
			.ThenByDescending(w => w.EffectiveFrom)
			.ToListAsync(ct);

	public async Task<IReadOnlyList<ClientWeekOff>> GetClientWeekOffsForAccountsAsync(IReadOnlyCollection<int> accountIds, CancellationToken ct = default) =>
		await db.ClientWeekOffs
			.AsNoTracking()
			.Where(w => accountIds.Contains(w.AccountId))
			.ToListAsync(ct);

	public Task<ClientWeekOff?> GetClientWeekOffByIdAsync(int clientWeekOffId, CancellationToken ct = default) =>
		db.ClientWeekOffs.FirstOrDefaultAsync(w => w.ClientWeekOffId == clientWeekOffId, ct);

	public async Task AddClientWeekOffAsync(ClientWeekOff weekOff, CancellationToken ct = default) =>
		await db.ClientWeekOffs.AddAsync(weekOff, ct);

	public void RemoveClientWeekOff(ClientWeekOff weekOff) => db.ClientWeekOffs.Remove(weekOff);

	// ---- Employee assignments ----

	public async Task<IReadOnlyList<EmployeeWeekOff>> GetAllAsync(CancellationToken ct = default) =>
		await db.EmployeeWeekOffs
			.AsNoTracking()
			.Include(w => w.Employee)
			.Include(w => w.CreatedBy)
			.OrderBy(w => w.Employee!.FullName)
			.ToListAsync(ct);

	public async Task<IReadOnlyList<EmployeeWeekOff>> GetForEmployeeAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
		await db.EmployeeWeekOffs
			.AsNoTracking()
			.Where(w => w.EmployeeId == employeeId
				&& w.EffectiveFrom <= to
				&& (w.EffectiveTo == null || w.EffectiveTo >= from))
			.ToListAsync(ct);

	public async Task<IReadOnlyList<EmployeeWeekOff>> GetAllForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
		await db.EmployeeWeekOffs
			.AsNoTracking()
			.Where(w => w.EmployeeId == employeeId)
			.ToListAsync(ct);

	public Task<int> CountForAccountAsync(int accountId, CancellationToken ct = default) =>
		db.EmployeeWeekOffs.CountAsync(w => w.AccountId == accountId, ct);

	public Task<EmployeeWeekOff?> GetByIdAsync(int employeeWeekOffId, CancellationToken ct = default) =>
		db.EmployeeWeekOffs.FirstOrDefaultAsync(w => w.EmployeeWeekOffId == employeeWeekOffId, ct);

	public async Task AddAsync(EmployeeWeekOff weekOff, CancellationToken ct = default) =>
		await db.EmployeeWeekOffs.AddAsync(weekOff, ct);

	public void Remove(EmployeeWeekOff weekOff) => db.EmployeeWeekOffs.Remove(weekOff);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
