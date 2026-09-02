namespace Meridian.Application.DTOs;

/// <summary>One logged line, with every level of the hierarchy resolved to a
/// display name — what the approval queue's expandable row detail needs.</summary>
public record ApprovalQueueLineDto(
	string DepartmentCode,
	string AccountName,
	string AccountType,
	string ProjectName,
	string ProjectCode,
	string ModuleName,
	string TaskName,
	string Classification,
	string? BillingCategory,
	decimal[] HoursByDay,
	string? Note
);

/// <summary>One week in an approver's queue, with everything the Level 1/2
/// Approvals screen needs in a single call — flags, the billable split, and
/// full line-level detail for the expandable row — rather than the frontend
/// making a separate request per row (the N+1 pattern the older, simpler
/// version of this screen used).</summary>
public record ApprovalQueueItemDto(
	string EmployeeCode,
	string FullName,
	string Designation,
	string DepartmentName,
	DateOnly WeekStartDate,
	DateTime? SubmittedAt,
	string Status,
	decimal TotalHours,
	decimal BillableHours,
	decimal NonBillableHours,
	decimal PartialBillableHours,
	int ProjectCount,
	int LineCount,
	IReadOnlyList<string> Flags,
	IReadOnlyList<ApprovalQueueLineDto> Lines,
	IReadOnlyList<DayTypeDto> DayTypes
);