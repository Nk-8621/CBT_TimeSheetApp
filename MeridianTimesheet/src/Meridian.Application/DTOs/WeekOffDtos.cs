namespace Meridian.Application.DTOs;

// WEEK OFFS (Nov 2026): Master Data -> Week Offs.

/// <summary>One row of the Week Offs list.</summary>
public record EmployeeWeekOffDto(
	int Id,
	string EmployeeCode,
	string EmployeeName,
	int AccountId,
	string ClientName,
	string[] WeekOffDays,
	DateOnly EffectiveFrom,
	DateOnly? EffectiveTo,
	string? CreatedByName);

/// <summary>Add or edit one record from the screen. WeekOffDays like ["Fri", "Sat"].</summary>
public record SaveEmployeeWeekOffRequest(
	string EmployeeCode,
	int AccountId,
	string[] WeekOffDays,
	DateOnly EffectiveFrom,
	DateOnly? EffectiveTo);

public record WeekOffOptionDto(string Value, string Label);

/// <summary>Drop-down choices for the Add Week Off drawer.</summary>
public record WeekOffOptionsDto(
	IReadOnlyList<WeekOffOptionDto> Employees,
	IReadOnlyList<WeekOffOptionDto> Clients);

/// <summary>Excel upload result. Nothing is saved when Errors is not empty.</summary>
public record WeekOffImportResultDto(int Imported, IReadOnlyList<string> Errors);
