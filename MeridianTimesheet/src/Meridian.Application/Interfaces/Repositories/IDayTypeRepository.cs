using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Interfaces.Repositories;

public interface IDayTypeRepository
{
    Task<IReadOnlyList<DayTypeOverride>> GetForWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);
    Task SetAsync(int employeeId, DateOnly date, DayType dayType, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
