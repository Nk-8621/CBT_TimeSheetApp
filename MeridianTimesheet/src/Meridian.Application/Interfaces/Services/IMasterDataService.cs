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
	Task<IReadOnlyList<TaskCategoryDto>> GetTaskCategoriesAsync(CancellationToken ct = default);

	Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
	Task<AccountDto> UpdateAccountAsync(int accountId, UpdateAccountRequest request, CancellationToken ct = default);

	/// <summary>If InitialModuleTaskCategoryCode is supplied, also auto-creates a
	/// starter "General" module under the new project pre-populated with that
	/// category's standard task list — matching the original wireframe's
	/// behavior of a new project being immediately usable. Pass null to skip
	/// (an empty project with no modules yet).</summary>
	Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default);
	Task<ProjectDto> UpdateProjectAsync(int projectId, UpdateProjectRequest request, CancellationToken ct = default);

	Task<ModuleDto> CreateModuleAsync(CreateModuleRequest request, CancellationToken ct = default);
	Task<ModuleDto> UpdateModuleAsync(int moduleId, UpdateModuleRequest request, CancellationToken ct = default);

	Task<WorkTaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default);
	Task<WorkTaskDto> UpdateTaskAsync(int taskId, UpdateTaskRequest request, CancellationToken ct = default);

	Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken ct = default);
	Task<HolidayDto> UpdateHolidayAsync(int holidayId, UpdateHolidayRequest request, CancellationToken ct = default);
	Task DeleteHolidayAsync(int holidayId, CancellationToken ct = default);
}
