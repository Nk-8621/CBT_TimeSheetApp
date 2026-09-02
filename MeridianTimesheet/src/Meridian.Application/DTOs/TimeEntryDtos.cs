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

public record DayTypeDto(DateOnly Date, string DayType, decimal CapacityHours);

public record SetDayTypeRequest(string DayType);
