namespace Meridian.Application.DTOs;

public record DepartmentDto(int Id, string Code, string Name, int? ParentDepartmentId);
public record LocationDto(int Id, string Code, string Name);
public record AccountDto(int Id, int DepartmentId, string Name, string AccountType);
public record ProjectDto(int Id, int AccountId, string Code, string Name, bool DefaultBillable, bool IsActive);
public record ModuleDto(int Id, int ProjectId, string Name, string TaskCategoryCode);
public record WorkTaskDto(int Id, int ModuleId, string Name);
//public record HolidayDto(int Id, DateOnly Date, string Name, string Location);
public record TaskCategoryDto(int Id, string Code, string Name);

// ---- Admin-only create/update requests (Section: Master Data CRUD) ----

public record CreateAccountRequest(int DepartmentId, string Name, string AccountType);
public record UpdateAccountRequest(int? DepartmentId, string? Name, string? AccountType);

/// <summary>InitialModuleTaskCategoryCode, if supplied, auto-creates a "General"
/// module under the new project, pre-populated with that category's standard
/// task list (see Common/TaskTemplates.cs) — matching the original wireframe's
/// behavior of a new project being immediately usable with no second setup
/// step. Pass null/omit to create an empty project with no modules yet.</summary>
public record CreateProjectRequest(int AccountId, string Code, string Name, bool DefaultBillable, string? InitialModuleTaskCategoryCode);
public record UpdateProjectRequest(int? AccountId, string? Code, string? Name, bool? DefaultBillable, bool? IsActive);

public record CreateModuleRequest(int ProjectId, string Name, string TaskCategoryCode);
public record UpdateModuleRequest(string? Name, string? TaskCategoryCode);

public record CreateTaskRequest(int ModuleId, string Name);
public record UpdateTaskRequest(string? Name);

public record CreateHolidayRequest(DateOnly HolidayDate, string Name, string Location, int? AccountId = null);
public record UpdateHolidayRequest(DateOnly? HolidayDate, string? Name, string? Location, int? AccountId = null);
public record HolidayDto(int HolidayId, DateOnly Date, string Name, string Location, int? AccountId);
