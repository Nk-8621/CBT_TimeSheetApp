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

    public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken ct = default) =>
        await db.Projects.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);

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
        return await query.ToListAsync(ct);
    }

    public Task<Holiday?> GetHolidayOnAsync(DateOnly date, CancellationToken ct = default) =>
        db.Holidays.AsNoTracking().FirstOrDefaultAsync(h => h.HolidayDate == date, ct);
}
