using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class LeaveRepository(MeridianDbContext db) : ILeaveRepository
{
    public async Task<IReadOnlyList<LeaveRecord>> GetForEmployeeAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await db.LeaveRecords
            .Where(l => l.EmployeeId == employeeId && l.LeaveDate >= from && l.LeaveDate <= to)
            .ToListAsync(ct);
}
