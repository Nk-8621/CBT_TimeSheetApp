namespace Meridian.Application.DTOs;

public record TeamComplianceRowDto(
	string EmployeeCode,
	string FullName,
	string Designation,
	string DepartmentName,
	string Status, // NotStarted, Draft, PendingL1, PendingL2, Approved, Rejected
	decimal TotalHours,
	bool HasLogged,
	decimal[] DailyHours,       // Mon..Sun
	string[] DailyDayTypes,     // Mon..Sun — W/WFH/L/H/O, drives the wireframe's per-day color coding
	string?[] DailyLeaveHalf,   // Mon..Sun — "LeaveFirstHalf"/"LeaveSecondHalf" where DailyDayTypes is "LH", else null
	decimal CapacityHours,
	decimal BillableHours,
	decimal PartialBillableHours,
	decimal NonBillableHours
);

public record ProjectHoursReportRowDto(
	int ProjectId,
	string ProjectCode,
	string ProjectName,
	string AccountName,
	decimal BillableHours,
	decimal PartialBillableHours,
	decimal NonBillableHours,
	decimal TotalHours,
	int EmployeeCount
);

/// <summary>One row in any of the Reports screen's five rollup tabs — the
/// shape is identical whether rolling up by department, account, project,
/// resource, or task, only what Key/SubLabel mean changes per tab.</summary>
public record ReportRollupRowDto(
	string Key,
	string SubLabel,
	decimal TotalHours,
	decimal BillableHours,
	decimal PartialBillableHours,
	decimal NonBillableHours,
	int ResourceCount
);

/// <summary>The full Reports screen payload — top KPIs plus all five rollup
/// views computed once from the same filtered dataset, so switching tabs on
/// the frontend doesn't need a fresh request.</summary>
public record ReportsSummaryDto(
	decimal ActualHours,
	decimal BillableHours,
	decimal TotalPartialBillableHours,
	decimal NonBillableHours,
	int ResourcesReporting,
	int ProjectsInScope,
	int TaskLineCount,
	IReadOnlyList<ReportRollupRowDto> DepartmentWise,
	IReadOnlyList<ReportRollupRowDto> AccountWise,
	IReadOnlyList<ReportRollupRowDto> ProjectWise,
	IReadOnlyList<ReportRollupRowDto> ResourceWise,
	IReadOnlyList<ReportRollupRowDto> TaskWise
);