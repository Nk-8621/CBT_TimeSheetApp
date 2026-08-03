using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

/// <summary>Read-only access to reference/lookup data (departments, accounts,
/// projects, modules, tasks, holidays). This data changes rarely, so it's
/// kept in one repository rather than five near-identical tiny ones.</summary>
public interface IMasterDataRepository
{
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Module>> GetModulesAsync(int? projectId = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkTask>> GetTasksAsync(int? moduleId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Holiday>> GetHolidaysAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);
    Task<Holiday?> GetHolidayOnAsync(DateOnly date, CancellationToken ct = default);
}
