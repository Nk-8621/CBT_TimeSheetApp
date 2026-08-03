using Meridian.Application.Common;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class DayTypeRepository(MeridianDbContext db) : IDayTypeRepository
{
    public async Task<IReadOnlyList<DayTypeOverride>> GetForWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default)
    {
        var days = WeekMath.WeekDays(weekStartDate);
        return await db.DayTypeOverrides
            .Where(d => d.EmployeeId == employeeId && days.Contains(d.EntryDate))
            .ToListAsync(ct);
    }

    public async Task SetAsync(int employeeId, DateOnly date, DayType dayType, CancellationToken ct = default)
    {
        var existing = await db.DayTypeOverrides
            .FirstOrDefaultAsync(d => d.EmployeeId == employeeId && d.EntryDate == date, ct);

        if (existing is not null)
        {
            existing.DayType = dayType;
        }
        else
        {
            await db.DayTypeOverrides.AddAsync(new DayTypeOverride
            {
                EmployeeId = employeeId,
                EntryDate = date,
                DayType = dayType,
                CreatedAt = DateTime.UtcNow,
            }, ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
