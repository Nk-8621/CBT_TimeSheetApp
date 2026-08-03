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
}
