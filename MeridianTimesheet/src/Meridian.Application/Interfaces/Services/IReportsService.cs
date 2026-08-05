using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>Aggregate, project-level reporting — separate from ITeamService
/// (which is about per-person compliance) and ITimesheetService (which is
/// about one person's own entries).</summary>
public interface IReportsService
{
	/// <summary>Hours broken down by project, for one week, summed across the
	/// given set of employees. The caller (controller) decides which
	/// employees are in scope — direct reports for a manager, everyone for
	/// an Admin — this method just aggregates whatever list it's given.</summary>
	Task<IReadOnlyList<ProjectHoursReportRowDto>> GetProjectHoursAsync(
		IReadOnlyList<int> employeeIds, DateOnly weekStart, CancellationToken ct = default);

	/// <summary>The full Reports screen: all 5 rollup dimensions computed once
	/// over the same filtered dataset (week range, optional department,
	/// approval-status set), matching the original wireframe's filters.
	/// approvalStatus is one of "all" | "sub" | "ok" (all timesheets /
	/// submitted-or-beyond / fully approved only).</summary>
	Task<ReportsSummaryDto> GetSummaryAsync(
		IReadOnlyList<int> employeeIds,
		DateOnly weekFrom,
		DateOnly weekTo,
		int? departmentId,
		string approvalStatus,
		CancellationToken ct = default);
}