using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Interfaces.Repositories;

public interface IDayTypeRequestRepository
{
    Task<DayTypeRequest?> GetByIdAsync(int dayTypeRequestId, CancellationToken ct = default);

    /// <summary>The one active (Pending or Approved) request for this employee/date,
    /// if any - used to stop a second request from being submitted for a date that
    /// already has one in flight.</summary>
    Task<DayTypeRequest?> GetActiveForDateAsync(int employeeId, DateOnly date, CancellationToken ct = default);

    /// <summary>Every request (any status) touching this employee's week, for display
    /// alongside the timesheet.</summary>
    Task<IReadOnlyList<DayTypeRequest>> GetForEmployeeWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);

    /// <summary>This employee's own requests, newest first - backs the Requests screen.</summary>
    Task<IReadOnlyList<DayTypeRequest>> GetForEmployeeAsync(int employeeId, CancellationToken ct = default);

    /// <summary>Every request currently Pending, across all employees - the service
    /// layer filters this down to whoever's Level 1 manager the caller is.</summary>
    Task<IReadOnlyList<DayTypeRequest>> GetPendingAsync(CancellationToken ct = default);

    /// <summary>Active (Pending or Approved) LeaveFirstHalf/LeaveSecondHalf/LeaveFull requests for this
    /// employee's week - the input DayTypeResolutionService needs to resolve leave
    /// days that originated in Meridian rather than KEKA.</summary>
    Task<IReadOnlyList<DayTypeRequest>> GetActiveLeaveForWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);

    Task AddAsync(DayTypeRequest request, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
