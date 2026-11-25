namespace Meridian.Application.DTOs;

// WEEK OFFS (Nov 2026): Master Data -> Week Offs.
// A client week off (client + days + period) is created first; employees are
// then assigned to the client for their own From / To dates.

/// <summary>An employee assigned to a client's week offs.</summary>
public record EmployeeWeekOffDto(
	int Id,
	string EmployeeCode,
	string EmployeeName,
	int AccountId,
	DateOnly EffectiveFrom,
	DateOnly? EffectiveTo,
	string? CreatedByName);

/// <summary>One row of the Week Offs list: a client's week-off days for a
/// period, with the employees assigned to that client during it.</summary>
public record ClientWeekOffDto(
	int Id,
	int AccountId,
	string ClientName,
	string[] WeekOffDays,
	DateOnly EffectiveFrom,
	DateOnly? EffectiveTo,
	IReadOnlyList<EmployeeWeekOffDto> Employees);

/// <summary>Add or edit a client week off. WeekOffDays like ["Fri", "Sat"].</summary>
public record SaveClientWeekOffRequest(
	int AccountId,
	string[] WeekOffDays,
	DateOnly EffectiveFrom,
	DateOnly? EffectiveTo);

/// <summary>Assign one or more employees to a client's week offs.</summary>
public record AssignEmployeesRequest(
	int AccountId,
	string[] EmployeeCodes,
	DateOnly EffectiveFrom,
	DateOnly? EffectiveTo);

/// <summary>Change one employee assignment's dates.</summary>
public record UpdateEmployeeWeekOffRequest(DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record WeekOffOptionDto(string Value, string Label);

/// <summary>Drop-down choices for the Week Offs drawers.</summary>
public record WeekOffOptionsDto(
	IReadOnlyList<WeekOffOptionDto> Employees,
	IReadOnlyList<WeekOffOptionDto> Clients);

/// <summary>Excel upload result. Nothing is saved when Errors is not empty.</summary>
public record WeekOffImportResultDto(int Imported, IReadOnlyList<string> Errors);
