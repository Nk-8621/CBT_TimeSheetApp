using Meridian.Application.Common;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Repositories;

public class DayTypeRequestRepository(MeridianDbContext db) : IDayTypeRequestRepository
{
    public Task<DayTypeRequest?> GetByIdAsync(int dayTypeRequestId, CancellationToken ct = default) =>
        db.DayTypeRequests.FirstOrDefaultAsync(r => r.DayTypeRequestId == dayTypeRequestId, ct);

    public Task<DayTypeRequest?> GetActiveForDateAsync(int employeeId, DateOnly date, CancellationToken ct = default) =>
        db.DayTypeRequests.FirstOrDefaultAsync(r =>
            r.EmployeeId == employeeId &&
            r.RequestDate == date &&
            (r.Status == DayTypeRequestStatus.Pending || r.Status == DayTypeRequestStatus.Approved), ct);

    public async Task<IReadOnlyList<DayTypeRequest>> GetForEmployeeWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default)
    {
        var days = WeekMath.WeekDays(weekStartDate);
        return await db.DayTypeRequests
            .Where(r => r.EmployeeId == employeeId && days.Contains(r.RequestDate))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DayTypeRequest>> GetForEmployeeAsync(int employeeId, CancellationToken ct = default) =>
        await db.DayTypeRequests
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DayTypeRequest>> GetPendingAsync(CancellationToken ct = default) =>
        await db.DayTypeRequests
            .Where(r => r.Status == DayTypeRequestStatus.Pending)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DayTypeRequest>> GetActiveLeaveForWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default)
    {
        var days = WeekMath.WeekDays(weekStartDate);
        return await db.DayTypeRequests
            .Where(r =>
                r.EmployeeId == employeeId &&
                days.Contains(r.RequestDate) &&
                (r.RequestType == DayTypeRequestType.LeaveFirstHalf || r.RequestType == DayTypeRequestType.LeaveSecondHalf || r.RequestType == DayTypeRequestType.LeaveFull) &&
                (r.Status == DayTypeRequestStatus.Pending || r.Status == DayTypeRequestStatus.Approved))
            .ToListAsync(ct);
    }

    public async Task AddAsync(DayTypeRequest request, CancellationToken ct = default) =>
        await db.DayTypeRequests.AddAsync(request, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
