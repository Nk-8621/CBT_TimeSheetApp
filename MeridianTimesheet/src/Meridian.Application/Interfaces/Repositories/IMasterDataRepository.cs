using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

/// <summary>Read-only access to reference/lookup data (departments, accounts,
/// projects, modules, tasks, holidays, project types). This data changes
/// rarely, so it's kept in one repository rather than several tiny ones.</summary>
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

	// ---- Project Type + its Module/Task template tree ----
	Task<IReadOnlyList<ProjectType>> GetProjectTypesAsync(CancellationToken ct = default);
	Task<ProjectType?> GetProjectTypeByIdAsync(int projectTypeId, CancellationToken ct = default);
	Task<ProjectType?> GetProjectTypeWithTemplatesByIdAsync(int projectTypeId, CancellationToken ct = default);
	Task<ProjectTypeModuleTemplate?> GetModuleTemplateByIdAsync(int moduleTemplateId, CancellationToken ct = default);
	Task<ProjectTypeTaskTemplate?> GetTaskTemplateByIdAsync(int taskTemplateId, CancellationToken ct = default);

	/// <summary>Every Project / Module currently pointing at this Project Type -
	/// used to reassign them to a replacement type before it's deleted.</summary>
	Task<IReadOnlyList<Project>> GetProjectsByProjectTypeIdAsync(int projectTypeId, CancellationToken ct = default);
	Task<IReadOnlyList<Module>> GetModulesByProjectTypeIdAsync(int projectTypeId, CancellationToken ct = default);

	// ---- Get by ID (tracked - needed before an update) ----
	Task<Account?> GetAccountByIdAsync(int accountId, CancellationToken ct = default);
	Task<Account?> GetAccountByNameAsync(string name, CancellationToken ct = default);
	Task<Project?> GetProjectByIdAsync(int projectId, CancellationToken ct = default);
	Task<Module?> GetModuleByIdAsync(int moduleId, CancellationToken ct = default);
	Task<WorkTask?> GetTaskByIdAsync(int taskId, CancellationToken ct = default);
	Task<Holiday?> GetHolidayByIdAsync(int holidayId, CancellationToken ct = default);
	Task<ProjectType?> GetProjectTypeByCodeAsync(string code, CancellationToken ct = default);
	Task<ProjectType?> GetProjectTypeByIdAsync(int projectTypeId, CancellationToken ct = default);
	Task<ProjectType?> GetProjectTypeWithTemplatesByIdAsync(int projectTypeId, CancellationToken ct = default);
	Task<ProjectTypeModuleTemplate?> GetModuleTemplateByIdAsync(int id, CancellationToken ct = default);
	Task<ProjectTypeTaskTemplate?> GetTaskTemplateByIdAsync(int id, CancellationToken ct = default);
	Task<IReadOnlyList<Project>> GetProjectsByProjectTypeIdAsync(int projectTypeId, CancellationToken ct = default);

	// ---- Mutations ----
	Task AddAccountAsync(Account account, CancellationToken ct = default);
	Task AddProjectAsync(Project project, CancellationToken ct = default);
	Task AddModuleAsync(Module module, CancellationToken ct = default);
	Task AddTaskAsync(WorkTask task, CancellationToken ct = default);
	Task AddHolidayAsync(Holiday holiday, CancellationToken ct = default);
	void RemoveHoliday(Holiday holiday);
	Task AddProjectTypeAsync(ProjectType projectType, CancellationToken ct = default);
	void RemoveProjectType(ProjectType projectType);
	Task AddModuleTemplateAsync(ProjectTypeModuleTemplate template, CancellationToken ct = default);
	void RemoveModuleTemplate(ProjectTypeModuleTemplate template);
	Task AddTaskTemplateAsync(ProjectTypeTaskTemplate template, CancellationToken ct = default);
	void RemoveTaskTemplate(ProjectTypeTaskTemplate template);

	Task AddProjectTypeAsync(ProjectType projectType, CancellationToken ct = default);
	void RemoveProjectType(ProjectType projectType);
	Task AddModuleTemplateAsync(ProjectTypeModuleTemplate template, CancellationToken ct = default);
	void RemoveModuleTemplate(ProjectTypeModuleTemplate template);
	Task AddTaskTemplateAsync(ProjectTypeTaskTemplate template, CancellationToken ct = default);
	void RemoveTaskTemplate(ProjectTypeTaskTemplate template);

	// ---- Project-wise resource allocation (admin reporting) ----
	/// <summary>Every Project with its EmployeeAllocations loaded (Employee NOT
	/// loaded) - enough to compute a headcount per project.</summary>
	Task<IReadOnlyList<Project>> GetProjectsWithAllocationsAsync(CancellationToken ct = default);
	/// <summary>One Project with its EmployeeAllocations AND each allocated
	/// Employee's Department loaded - backs the "who's on this project" drill-down.</summary>
	Task<Project?> GetProjectWithAllocationsByIdAsync(int projectId, CancellationToken ct = default);

	Task SaveChangesAsync(CancellationToken ct = default);
}
