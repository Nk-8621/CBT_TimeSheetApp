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
	Task<IReadOnlyList<ProjectTypeDto>> GetProjectTypesAsync(CancellationToken ct = default);
	Task<IReadOnlyList<ProjectTypeWithTemplateDto>> GetProjectTypesWithTemplatesAsync(CancellationToken ct = default);

	Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
	Task<AccountDto> UpdateAccountAsync(int accountId, UpdateAccountRequest request, CancellationToken ct = default);

	/// <summary>If ProjectTypeId is supplied, auto-generates the full real
	/// Module(L1)/Task(L2) tree from that type's template under the new
	/// project - matching the original wireframe's behavior of a new project
	/// being immediately usable. Pass null to skip (an empty project with no
	/// modules yet).</summary>
	Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default);
	Task<ProjectDto> UpdateProjectAsync(int projectId, UpdateProjectRequest request, CancellationToken ct = default);

	Task<ModuleDto> CreateModuleAsync(CreateModuleRequest request, CancellationToken ct = default);
	Task<ModuleDto> UpdateModuleAsync(int moduleId, UpdateModuleRequest request, CancellationToken ct = default);

	Task<WorkTaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default);
	Task<WorkTaskDto> UpdateTaskAsync(int taskId, UpdateTaskRequest request, CancellationToken ct = default);

	Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken ct = default);
	Task<HolidayDto> UpdateHolidayAsync(int holidayId, UpdateHolidayRequest request, CancellationToken ct = default);
	Task DeleteHolidayAsync(int holidayId, CancellationToken ct = default);

	// ---- Project Type template management (Admin only) ----
	Task<ProjectTypeDto> CreateProjectTypeAsync(CreateProjectTypeRequest request, CancellationToken ct = default);
	Task<ProjectTypeDto> UpdateProjectTypeAsync(int projectTypeId, UpdateProjectTypeRequest request, CancellationToken ct = default);
	/// <summary>Requires ReplacementProjectTypeId whenever any Project still
	/// references projectTypeId - reassigns them all first, then deletes.</summary>
	Task DeleteProjectTypeAsync(int projectTypeId, DeleteProjectTypeRequest request, CancellationToken ct = default);
	Task<ProjectTypeModuleTemplateDto> CreateModuleTemplateAsync(CreateProjectTypeModuleTemplateRequest request, CancellationToken ct = default);
	Task<ProjectTypeModuleTemplateDto> UpdateModuleTemplateAsync(int id, UpdateProjectTypeModuleTemplateRequest request, CancellationToken ct = default);
	Task DeleteModuleTemplateAsync(int id, CancellationToken ct = default);
	Task<ProjectTypeTaskTemplateDto> CreateTaskTemplateAsync(CreateProjectTypeTaskTemplateRequest request, CancellationToken ct = default);
	Task<ProjectTypeTaskTemplateDto> UpdateTaskTemplateAsync(int id, UpdateProjectTypeTaskTemplateRequest request, CancellationToken ct = default);
	Task DeleteTaskTemplateAsync(int id, CancellationToken ct = default);

	// ---- "Others" quick-add (employee-reachable, not admin-only) ----
	Task<ProjectDto> QuickAddProjectAsync(QuickAddProjectRequest request, CancellationToken ct = default);
	Task<ModuleDto> QuickAddModuleAsync(QuickAddModuleRequest request, CancellationToken ct = default);
	Task<WorkTaskDto> QuickAddTaskAsync(QuickAddTaskRequest request, CancellationToken ct = default);
}
