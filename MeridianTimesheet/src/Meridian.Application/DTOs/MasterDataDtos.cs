namespace Meridian.Application.DTOs;

public record DepartmentDto(int Id, string Code, string Name, int? ParentDepartmentId);
public record LocationDto(int Id, string Code, string Name);
public record AccountDto(int Id, int DepartmentId, string Name, string AccountType);
public record ProjectDto(int Id, int AccountId, string Code, string Name, bool DefaultBillable);
public record ModuleDto(int Id, int ProjectId, string Name, string TaskCategoryCode);
public record WorkTaskDto(int Id, int ModuleId, string Name);
public record HolidayDto(DateOnly Date, string Name, string Location);
