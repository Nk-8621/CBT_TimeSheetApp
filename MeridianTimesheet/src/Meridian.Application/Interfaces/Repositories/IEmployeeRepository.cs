using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> GetByCodeAsync(string employeeCode, CancellationToken ct = default);
    Task<Employee?> GetByIdAsync(int employeeId, CancellationToken ct = default);
    Task<Employee?> GetByEntraObjectIdAsync(Guid entraObjectId, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetDirectReportsAsync(int managerEmployeeId, CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetAllAsync(CancellationToken ct = default);
    Task<bool> HasRoleAsync(int employeeId, string roleCode, CancellationToken ct = default);
	Task SaveChangesAsync(CancellationToken ct = default);
	Task<Employee?> GetByCodeOrEmailAsync(string identifier, CancellationToken ct = default);
}
