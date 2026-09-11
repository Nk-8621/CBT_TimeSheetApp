using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

public interface IMasterDataService
{
	Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<ModuleDto>> GetModulesAsync(int? projectId = null, CancellationToken ct = default);
	Task<IReadOnlyList<WorkTaskDto>> GetTasksAsync(int? moduleId = null, CancellationToken ct = default);
	Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(CancellationToken ct = default);

	// ---- Project Type + its Module/Task template tree ----
	Task<IReadOnlyList<ProjectTypeDto>> GetProjectTypesAsync(CancellationToken ct = default);
	Task<ProjectTypeWithTemplateDto> GetProjectTypeWithTemplateAsync(int projectTypeId, CancellationToken ct = default);
	Task<ProjectTypeDto> CreateProjectTypeAsync(CreateProjectTypeRequest request, CancellationToken ct = default);
	Task<ProjectTypeDto> UpdateProjectTypeAsync(int projectTypeId, UpdateProjectTypeRequest request, CancellationToken ct = default);
	/// <summary>Fails with a clear error if the type is still in use and no
	/// ReplacementProjectTypeId was supplied to reassign those Projects/Modules first.</summary>
	Task DeleteProjectTypeAsync(int projectTypeId, DeleteProjectTypeRequest request, CancellationToken ct = default);

	Task<ProjectTypeModuleTemplateDto> CreateModuleTemplateAsync(CreateProjectTypeModuleTemplateRequest request, CancellationToken ct = default);
	Task<ProjectTypeModuleTemplateDto> UpdateModuleTemplateAsync(int moduleTemplateId, UpdateProjectTypeModuleTemplateRequest request, CancellationToken ct = default);
	Task DeleteModuleTemplateAsync(int moduleTemplateId, CancellationToken ct = default);

	Task<ProjectTypeTaskTemplateDto> CreateTaskTemplateAsync(CreateProjectTypeTaskTemplateRequest request, CancellationToken ct = default);
	Task<ProjectTypeTaskTemplateDto> UpdateTaskTemplateAsync(int taskTemplateId, UpdateProjectTypeTaskTemplateRequest request, CancellationToken ct = default);
	Task DeleteTaskTemplateAsync(int taskTemplateId, CancellationToken ct = default);

	Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
	Task<AccountDto> UpdateAccountAsync(int accountId, UpdateAccountRequest request, CancellationToken ct = default);

	/// <summary>If ProjectTypeId is supplied, the new Project's full Module/Task
	/// template tree is generated immediately from that Project Type - matching
	/// the original wireframe's behavior of a new project being immediately
	/// usable with no second setup step. Pass null to create an empty project.</summary>
	Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default);
	Task<ProjectDto> UpdateProjectAsync(int projectId, UpdateProjectRequest request, CancellationToken ct = default);

	/// <summary>Explicit re-sync for a project that already has a Project Type
	/// set (the automatic sync in UpdateProjectAsync only fires the moment a
	/// type is first assigned). Re-applies the same merge-only rule: adds
	/// whichever of the type's template Modules/Tasks the project is still
	/// missing, matched by name, never touching or removing anything that
	/// already exists. Fails if the project has no Project Type yet.</summary>
	Task<ProjectDto> SyncProjectModulesFromTemplateAsync(int projectId, CancellationToken ct = default);

	Task<ModuleDto> CreateModuleAsync(CreateModuleRequest request, CancellationToken ct = default);
	Task<ModuleDto> UpdateModuleAsync(int moduleId, UpdateModuleRequest request, CancellationToken ct = default);

	Task<WorkTaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default);
	Task<WorkTaskDto> UpdateTaskAsync(int taskId, UpdateTaskRequest request, CancellationToken ct = default);

	/// <summary>Self-service "Others" quick-add from the timesheet entry screen -
	/// lands in the "Pending Classification" internal account, flagged
	/// NeedsReview, for an admin to properly classify later.</summary>
	Task<ProjectDto> QuickAddProjectAsync(QuickAddProjectRequest request, CancellationToken ct = default);
	Task<ModuleDto> QuickAddModuleAsync(QuickAddModuleRequest request, CancellationToken ct = default);
	Task<WorkTaskDto> QuickAddTaskAsync(QuickAddTaskRequest request, CancellationToken ct = default);

	Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken ct = default);
	Task<HolidayDto> UpdateHolidayAsync(int holidayId, UpdateHolidayRequest request, CancellationToken ct = default);
	Task DeleteHolidayAsync(int holidayId, CancellationToken ct = default);

	// ---- Project-wise resource allocation (admin reporting) ----
	Task<IReadOnlyList<ProjectResourceAllocationDto>> GetProjectResourceAllocationsAsync(CancellationToken ct = default);
	Task<IReadOnlyList<AllocatedEmployeeDto>> GetAllocatedEmployeesAsync(int projectId, CancellationToken ct = default);
}
