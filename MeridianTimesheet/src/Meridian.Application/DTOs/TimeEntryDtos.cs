namespace Meridian.Application.DTOs;

public record TimeEntryDto(
    int Id,
    string EmployeeCode,
    DateOnly WeekStartDate,
    int ProjectId,
    int ModuleId,
    int TaskId,
    string Classification,
    string? BillingCategory,
    string? Note,
    decimal[] HoursByDay // Monday..Sunday
);

public record CreateTimeEntryRequest(
    int ProjectId,
    int ModuleId,
    int TaskId,
    string Classification,
    string? BillingCategory,
    string? Note,
    decimal[] HoursByDay
);

public record UpdateTimeEntryRequest(
    int? ProjectId,
    int? ModuleId,
    int? TaskId,
    string? Classification,
    string? BillingCategory,
    string? Note,
    decimal[]? HoursByDay
);

public record DayTypeDto(DateOnly Date, string DayType, decimal CapacityHours,
	// Only set when DayType == "LH" (half-day leave) - which specific half
	// ("LeaveFirstHalf" / "LeaveSecondHalf") drove the resolution, so callers
	// like Team Compliance can render the correct half of the day box. Null
	// for every other day type.
	string? RequestType = null);

public record SetDayTypeRequest(string DayType);
