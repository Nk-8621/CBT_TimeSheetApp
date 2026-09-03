namespace Meridian.Application.DTOs;

public record ApprovalEventDto(string Text, string? Meta, string? Status, DateTime Timestamp);

public record WeekRecordDto(
    string EmployeeCode,
    DateOnly WeekStartDate,
    string Status,
    DateTime? SubmittedAt,
    string? RejectedByName,
    string? RejectionReason,
    IReadOnlyList<ApprovalEventDto> Trail
);

public record RejectWeekRequest(string Reason);

public record WeekValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool CanSubmit => Errors.Count == 0;
}

public record WeekSummaryDto(
    WeekRecordDto Week,
    IReadOnlyList<TimeEntryDto> Entries,
    IReadOnlyList<DayTypeDto> DayTypes,
    decimal TotalHours,
    decimal BillableHours,
    decimal PartialBillableHours,
    decimal CapacityHours,
    IReadOnlyList<DayTypeRequestDto> DayTypeRequests
);

public record WeekHistoryItemDto(
	DateOnly WeekStartDate,
	string Status,
	decimal TotalHours,
	decimal BillableHours,
	decimal PartialBillableHours,
	DateTime? SubmittedAt
);
