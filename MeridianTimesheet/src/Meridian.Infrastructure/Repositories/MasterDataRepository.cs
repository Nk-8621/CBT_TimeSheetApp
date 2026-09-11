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

	// Returns ALL projects, active or not - the Master Data admin screen needs
	// to see inactive projects too (otherwise deactivating one is a one-way
	// trip with no way to ever see or reactivate it again). Screens that log
	// NEW time (e.g. the Add Task Line dropdown) filter to active-only
	// client-side instead.
	public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken ct = default) =>
		await db.Projects.AsNoTracking()
			.Include(p => p.ProjectType)
			.Include(p => p.ProjectLeadEmployee)
			.Include(p => p.ProjectManagerEmployee)
			.Include(p => p.DeliveryHeadEmployee)
			.ToListAsync(ct);

	public async Task<IReadOnlyList<Module>> GetModulesAsync(int? projectId = null, CancellationToken ct = default)
	{
		var query = db.Modules.AsNoTracking().Include(m => m.ProjectType).AsQueryable();
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

	// ---- Project Type + templates ----

	public async Task<IReadOnlyList<ProjectType>> GetProjectTypesAsync(CancellationToken ct = default) =>
		await db.ProjectTypes.AsNoTracking().ToListAsync(ct);

	public Task<ProjectType?> GetProjectTypeByIdAsync(int projectTypeId, CancellationToken ct = default) =>
		db.ProjectTypes.FirstOrDefaultAsync(t => t.ProjectTypeId == projectTypeId, ct);

	public Task<ProjectType?> GetProjectTypeWithTemplatesByIdAsync(int projectTypeId, CancellationToken ct = default) =>
		db.ProjectTypes
			.Include(t => t.ModuleTemplates.OrderBy(m => m.SortOrder))
			.ThenInclude(m => m.TaskTemplates.OrderBy(x => x.SortOrder))
			.AsSplitQuery()
			.FirstOrDefaultAsync(t => t.ProjectTypeId == projectTypeId, ct);

	public Task<ProjectTypeModuleTemplate?> GetModuleTemplateByIdAsync(int moduleTemplateId, CancellationToken ct = default) =>
		db.ProjectTypeModuleTemplates.FirstOrDefaultAsync(m => m.ProjectTypeModuleTemplateId == moduleTemplateId, ct);

	public Task<ProjectTypeTaskTemplate?> GetTaskTemplateByIdAsync(int taskTemplateId, CancellationToken ct = default) =>
		db.ProjectTypeTaskTemplates.FirstOrDefaultAsync(t => t.ProjectTypeTaskTemplateId == taskTemplateId, ct);

	public async Task<IReadOnlyList<Project>> GetProjectsByProjectTypeIdAsync(int projectTypeId, CancellationToken ct = default) =>
		await db.Projects.Where(p => p.ProjectTypeId == projectTypeId).ToListAsync(ct);

	public async Task<IReadOnlyList<Module>> GetModulesByProjectTypeIdAsync(int projectTypeId, CancellationToken ct = default) =>
		await db.Modules.Where(m => m.ProjectTypeId == projectTypeId).ToListAsync(ct);

	// ---- Get by ID (tracked - the service mutates these directly for updates) ----
	public Task<Account?> GetAccountByIdAsync(int accountId, CancellationToken ct = default) =>
		db.Accounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

	public Task<Project?> GetProjectByIdAsync(int projectId, CancellationToken ct = default) =>
		db.Projects
			.Include(p => p.ProjectType)
			.Include(p => p.ProjectLeadEmployee)
			.Include(p => p.ProjectManagerEmployee)
			.Include(p => p.DeliveryHeadEmployee)
			.FirstOrDefaultAsync(p => p.ProjectId == projectId, ct);

	public Task<Module?> GetModuleByIdAsync(int moduleId, CancellationToken ct = default) =>
		db.Modules.Include(m => m.ProjectType).FirstOrDefaultAsync(m => m.ModuleId == moduleId, ct);

	public Task<WorkTask?> GetTaskByIdAsync(int taskId, CancellationToken ct = default) =>
		db.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId, ct);

	public Task<Holiday?> GetHolidayByIdAsync(int holidayId, CancellationToken ct = default) =>
		db.Holidays.FirstOrDefaultAsync(h => h.HolidayId == holidayId, ct);

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

	public async Task AddProjectTypeAsync(ProjectType projectType, CancellationToken ct = default) =>
		await db.ProjectTypes.AddAsync(projectType, ct);

	public void RemoveProjectType(ProjectType projectType) => db.ProjectTypes.Remove(projectType);

	public async Task AddModuleTemplateAsync(ProjectTypeModuleTemplate template, CancellationToken ct = default) =>
		await db.ProjectTypeModuleTemplates.AddAsync(template, ct);

	public void RemoveModuleTemplate(ProjectTypeModuleTemplate template) => db.ProjectTypeModuleTemplates.Remove(template);

	public async Task AddTaskTemplateAsync(ProjectTypeTaskTemplate template, CancellationToken ct = default) =>
		await db.ProjectTypeTaskTemplates.AddAsync(template, ct);

	public void RemoveTaskTemplate(ProjectTypeTaskTemplate template) => db.ProjectTypeTaskTemplates.Remove(template);

	// ---- Project-wise resource allocation ----

	public async Task<IReadOnlyList<Project>> GetProjectsWithAllocationsAsync(CancellationToken ct = default) =>
		await db.Projects.AsNoTracking()
			.Include(p => p.EmployeeAllocations)
			.ToListAsync(ct);

	public Task<Project?> GetProjectWithAllocationsByIdAsync(int projectId, CancellationToken ct = default) =>
		db.Projects.AsNoTracking()
			.Include(p => p.EmployeeAllocations).ThenInclude(a => a.Employee).ThenInclude(e => e!.Department)
			.AsSplitQuery()
			.FirstOrDefaultAsync(p => p.ProjectId == projectId, ct);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
