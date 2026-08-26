using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class MasterDataRepository(MeridianDbContext db) : IMasterDataRepository
{
	public async Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken ct = default) =>
		await db.Departments.AsNoTracking().ToListAsync(ct);

	public async Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken ct = default) =>
		await db.Locations.AsNoTracking().ToListAsync(ct);

	public async Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default) =>
		await db.Accounts.AsNoTracking().ToListAsync(ct);

	// Returns ALL projects, active or not — the Master Data admin screen needs
	// to see inactive projects too (otherwise deactivating one is a one-way
	// trip with no way to ever see or reactivate it again). Screens that log
	// NEW time (e.g. the Add Task Line dropdown) filter to active-only
	// client-side instead.
	public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken ct = default) =>
		await db.Projects.AsNoTracking().ToListAsync(ct);

	public async Task<IReadOnlyList<Module>> GetModulesAsync(int? projectId = null, CancellationToken ct = default)
	{
		var query = db.Modules.AsNoTracking().Include(m => m.TaskCategory).AsQueryable();
		if (projectId is int p) query = query.Where(m => m.ProjectId == p);
		return await query.ToListAsync(ct);
	}

	public async Task<IReadOnlyList<WorkTask>> GetTasksAsync(int? moduleId = null, CancellationToken ct = default)
	{
		var query = db.Tasks.AsNoTracking().AsQueryable();
		if (moduleId is int m) query = query.Where(t => t.ModuleId == m);
		return await query.ToListAsync(ct);
	}

	public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
	{
		var query = db.Holidays.AsNoTracking().AsQueryable();
		if (from is DateOnly f) query = query.Where(h => h.HolidayDate >= f);
		if (to is DateOnly t) query = query.Where(h => h.HolidayDate <= t);
		return await query.OrderBy(h => h.HolidayDate).ToListAsync(ct);
	}

	public Task<Holiday?> GetHolidayOnAsync(DateOnly date, int? accountId, CancellationToken ct = default) =>
	db.Holidays.FirstOrDefaultAsync(h => h.HolidayDate == date && (h.AccountId == null || h.AccountId == accountId), ct);

	public async Task<IReadOnlyList<TaskCategory>> GetTaskCategoriesAsync(CancellationToken ct = default) =>
		await db.TaskCategories.AsNoTracking().ToListAsync(ct);

	// ---- Get by ID (tracked — the service mutates these directly for updates) ----
	public Task<Account?> GetAccountByIdAsync(int accountId, CancellationToken ct = default) =>
		db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

	public Task<Project?> GetProjectByIdAsync(int projectId, CancellationToken ct = default) =>
		db.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId, ct);

	public Task<Module?> GetModuleByIdAsync(int moduleId, CancellationToken ct = default) =>
		db.Modules.FirstOrDefaultAsync(m => m.ModuleId == moduleId, ct);

	public Task<WorkTask?> GetTaskByIdAsync(int taskId, CancellationToken ct = default) =>
		db.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId, ct);

	public Task<Holiday?> GetHolidayByIdAsync(int holidayId, CancellationToken ct = default) =>
		db.Holidays.FirstOrDefaultAsync(h => h.HolidayId == holidayId, ct);

	public Task<TaskCategory?> GetTaskCategoryByCodeAsync(string code, CancellationToken ct = default) =>
		db.TaskCategories.FirstOrDefaultAsync(c => c.Code == code, ct);

	// ---- Mutations ----
	public async Task AddAccountAsync(Account account, CancellationToken ct = default) =>
		await db.Accounts.AddAsync(account, ct);

	public async Task AddProjectAsync(Project project, CancellationToken ct = default) =>
		await db.Projects.AddAsync(project, ct);

	public async Task AddModuleAsync(Module module, CancellationToken ct = default) =>
		await db.Modules.AddAsync(module, ct);

	public async Task AddTaskAsync(WorkTask task, CancellationToken ct = default) =>
		await db.Tasks.AddAsync(task, ct);

	public async Task AddHolidayAsync(Holiday holiday, CancellationToken ct = default) =>
		await db.Holidays.AddAsync(holiday, ct);

	public void RemoveHoliday(Holiday holiday) => db.Holidays.Remove(holiday);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}