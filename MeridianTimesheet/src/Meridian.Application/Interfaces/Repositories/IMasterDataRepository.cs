using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

/// <summary>Read-only access to reference/lookup data (departments, accounts,
/// projects, modules, tasks, holidays). This data changes rarely, so it's
/// kept in one repository rather than five near-identical tiny ones.</summary>
public interface IMasterDataRepository
{
	// ---- Read (all entities) ----
	Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<Module>> GetModulesAsync(int? projectId = null, CancellationToken ct = default);
	Task<IReadOnlyList<WorkTask>> GetTasksAsync(int? moduleId = null, CancellationToken ct = default);
	Task<IReadOnlyList<Holiday>> GetHolidaysAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);
	Task<Holiday?> GetHolidayOnAsync(DateOnly date, int? accountId, CancellationToken ct = default);
	Task<IReadOnlyList<TaskCategory>> GetTaskCategoriesAsync(CancellationToken ct = default);

	// ---- Get by ID (needed before an update) ----
	Task<Account?> GetAccountByIdAsync(int accountId, CancellationToken ct = default);
	Task<Project?> GetProjectByIdAsync(int projectId, CancellationToken ct = default);
	Task<Module?> GetModuleByIdAsync(int moduleId, CancellationToken ct = default);
	Task<WorkTask?> GetTaskByIdAsync(int taskId, CancellationToken ct = default);
	Task<Holiday?> GetHolidayByIdAsync(int holidayId, CancellationToken ct = default);
	Task<TaskCategory?> GetTaskCategoryByCodeAsync(string code, CancellationToken ct = default);

	// ---- Mutations ----
	Task AddAccountAsync(Account account, CancellationToken ct = default);
	Task AddProjectAsync(Project project, CancellationToken ct = default);
	Task AddModuleAsync(Module module, CancellationToken ct = default);
	Task AddTaskAsync(WorkTask task, CancellationToken ct = default);
	Task AddHolidayAsync(Holiday holiday, CancellationToken ct = default);
	void RemoveHoliday(Holiday holiday);

	Task SaveChangesAsync(CancellationToken ct = default);
}
