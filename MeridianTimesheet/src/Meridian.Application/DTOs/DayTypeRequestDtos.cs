namespace Meridian.Application.DTOs;

public record DayTypeRequestDto(
    int Id,
    string EmployeeCode,
    string EmployeeName,
    DateOnly RequestDate,
    string RequestType,   // WFH | LeaveFirstHalf | LeaveSecondHalf | LeaveFull
    string Status,        // Pending | Approved | Rejected
    string? Note,
    DateTime SubmittedAt,
    string? ApproverName,
    DateTime? DecidedAt,
    string? DecisionComment
);

public record CreateDayTypeRequestRequest(DateOnly Date, string RequestType, string? Note);

public record DecideDayTypeRequestRequest(string? Comment);
