using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

/// <summary>WEEK OFFS (Nov 2026): see IEmployeeWeekOffRepository.</summary>
public class EmployeeWeekOffRepository(MeridianDbContext db) : IEmployeeWeekOffRepository
{
	public async Task<IReadOnlyList<EmployeeWeekOff>> GetAllAsync(CancellationToken ct = default) =>
		await db.EmployeeWeekOffs
			.AsNoTracking()
			.Include(w => w.Employee)
			.Include(w => w.Account)
			.Include(w => w.CreatedBy)
			.OrderByDescending(w => w.EffectiveFrom)
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

	public Task<EmployeeWeekOff?> GetByIdAsync(int employeeWeekOffId, CancellationToken ct = default) =>
		db.EmployeeWeekOffs.FirstOrDefaultAsync(w => w.EmployeeWeekOffId == employeeWeekOffId, ct);

	public async Task AddAsync(EmployeeWeekOff weekOff, CancellationToken ct = default) =>
		await db.EmployeeWeekOffs.AddAsync(weekOff, ct);

	public void Remove(EmployeeWeekOff weekOff) => db.EmployeeWeekOffs.Remove(weekOff);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
