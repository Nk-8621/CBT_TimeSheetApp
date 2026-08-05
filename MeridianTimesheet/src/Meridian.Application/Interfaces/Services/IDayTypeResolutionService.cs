using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>
/// Resolves each day of a week to its effective day type (Working / WFH /
/// Leave / Holiday / Weekly-off) and capacity. Extracted as its own service
/// because three different consumers need the exact same resolution logic:
/// TimesheetService (My Timesheet), TeamService (Team Compliance's daily
/// breakdown), and WeekApprovalService (the approval queue's flags and
/// per-day detail) — duplicating this in three places would let them drift
/// out of sync with each other.
/// </summary>
public interface IDayTypeResolutionService
{
	Task<List<DayTypeDto>> ResolveWeekAsync(int employeeId, DateOnly weekStartDate, CancellationToken ct = default);
}