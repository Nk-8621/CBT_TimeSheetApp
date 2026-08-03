using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;

namespace Meridian.Application.Services;

public class MasterDataService(IMasterDataRepository repository) : IMasterDataService
{
    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        var departments = await repository.GetDepartmentsAsync(ct);
        return departments.Select(d => new DepartmentDto(d.DepartmentId, d.Code, d.Name, d.ParentDepartmentId)).ToList();
    }

    public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default)
    {
        var locations = await repository.GetLocationsAsync(ct);
        return locations.Select(l => new LocationDto(l.LocationId, l.Code, l.Name)).ToList();
    }

    public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken ct = default)
    {
        var accounts = await repository.GetAccountsAsync(ct);
        return accounts.Select(a => new AccountDto(a.AccountId, a.DepartmentId, a.Name, a.AccountType.ToString())).ToList();
    }

    public async Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken ct = default)
    {
        var projects = await repository.GetProjectsAsync(ct);
        return projects.Select(p => new ProjectDto(p.ProjectId, p.AccountId, p.Code, p.Name, p.DefaultBillable)).ToList();
    }

    public async Task<IReadOnlyList<ModuleDto>> GetModulesAsync(int? projectId = null, CancellationToken ct = default)
    {
        var modules = await repository.GetModulesAsync(projectId, ct);
        return modules.Select(m => new ModuleDto(m.ModuleId, m.ProjectId, m.Name, m.TaskCategory?.Code ?? "")).ToList();
    }

    public async Task<IReadOnlyList<WorkTaskDto>> GetTasksAsync(int? moduleId = null, CancellationToken ct = default)
    {
        var tasks = await repository.GetTasksAsync(moduleId, ct);
        return tasks.Select(t => new WorkTaskDto(t.TaskId, t.ModuleId, t.Name)).ToList();
    }
}
