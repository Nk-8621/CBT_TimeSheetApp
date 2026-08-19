using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class EmployeeRepository(MeridianDbContext db) : IEmployeeRepository
{
    public Task<Employee?> GetByCodeAsync(string employeeCode, CancellationToken ct = default) =>
        db.Employees.FirstOrDefaultAsync(e => e.EmployeeCode == employeeCode, ct);

    public Task<Employee?> GetByIdAsync(int employeeId, CancellationToken ct = default) =>
        db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);

    public Task<Employee?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default) =>
        db.Employees.FirstOrDefaultAsync(e => e.EntraObjectId == entraObjectId, ct);

    public async Task<bool> HasRoleAsync(int employeeId, string roleCode, CancellationToken ct = default) =>
        await db.EmployeeRoles
            .Include(er => er.Role)
            .AnyAsync(er => er.EmployeeId == employeeId && er.Role!.Code == roleCode, ct);

    public async Task<IReadOnlyList<Employee>> GetDirectReportsAsync(int managerEmployeeId, CancellationToken ct = default) =>
        await db.Employees.Where(e => e.ManagerEmployeeId == managerEmployeeId).ToListAsync(ct);

    public async Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken ct = default) =>
        await db.Employees.AsNoTracking().ToListAsync(ct);

	public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
