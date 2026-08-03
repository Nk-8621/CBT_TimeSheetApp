using Meridian.Domain.Entities;

namespace Meridian.Application.Interfaces.Repositories;

/// <summary>Read-only — leave is synced in from KEKA, never originated in Meridian.</summary>
public interface ILeaveRepository
{
    Task<IReadOnlyList<LeaveRecord>> GetForEmployeeAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
