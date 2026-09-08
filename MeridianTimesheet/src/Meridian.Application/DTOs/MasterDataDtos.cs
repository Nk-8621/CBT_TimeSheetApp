namespace Meridian.Application.DTOs;

public record DepartmentDto(int Id, string Code, string Name, int? ParentDepartmentId);
public record LocationDto(int Id, string Code, string Name);
public record AccountDto(int Id, int DepartmentId, string Name, string AccountType);

public record ProjectDto(
	int Id, int AccountId, string Code, string Name, bool DefaultBillable, bool IsActive,
	int? ProjectTypeId, string? ProjectTypeName, string? ProjectTech, string? BillingType,
	string? CustomerPO, string? Notes, bool NeedsReview,
	int? ProjectLeadEmployeeId, string? ProjectLeadEmployeeName,
	int? ProjectManagerEmployeeId, string? ProjectManagerEmployeeName,
	int? DeliveryHeadEmployeeId, string? DeliveryHeadEmployeeName);

public record ModuleDto(int Id, int ProjectId, string Name, int? ProjectTypeId, string? ProjectTypeCode);
public record WorkTaskDto(int Id, int ModuleId, string Name);
//public record HolidayDto(int Id, DateOnly Date, string Name, string Location);

// ---- Project Type + its Level-1/Level-2 template ----

public record ProjectTypeDto(int Id, string Code, string Name);
public record ProjectTypeTaskTemplateDto(int Id, string Name, int SortOrder);
public record ProjectTypeModuleTemplateDto(int Id, string Name, int SortOrder, IReadOnlyList<ProjectTypeTaskTemplateDto> Tasks);
/// <summary>Full tree for the admin Project Type template-management screen.</summary>
public record ProjectTypeWithTemplateDto(int Id, string Code, string Name, IReadOnlyList<ProjectTypeModuleTemplateDto> Modules);

public record CreateProjectTypeRequest(string Code, string Name);
public record UpdateProjectTypeRequest(string? Code, string? Name);
/// <summary>Deleting a Project Type that any Project still uses requires
/// picking a replacement up front - every affected Project gets reassigned
/// to ReplacementProjectTypeId before the delete proceeds, so nothing is
/// ever left orphaned.</summary>
public record DeleteProjectTypeRequest(int? ReplacementProjectTypeId);

public record CreateProjectTypeModuleTemplateRequest(int ProjectTypeId, string Name, int SortOrder);
public record UpdateProjectTypeModuleTemplateRequest(string? Name, int? SortOrder);
public record CreateProjectTypeTaskTemplateRequest(int ProjectTypeModuleTemplateId, string Name, int SortOrder);
public record UpdateProjectTypeTaskTemplateRequest(string? Name, int? SortOrder);

// ---- Admin-only create/update requests (Section: Master Data CRUD) ----

public record CreateAccountRequest(int DepartmentId, string Name, string AccountType);
public record UpdateAccountRequest(int? DepartmentId, string? Name, string? AccountType);

/// <summary>ProjectTypeId, if supplied, auto-generates the full real Module
/// (Level-1) / Task (Level-2) tree from that type's template under the new
/// project - see ProjectTypeModuleTemplate/ProjectTypeTaskTemplate and
/// MasterDataService.GenerateModulesFromProjectTypeAsync. Pass null/omit to
/// create an empty project with no modules yet. If the referenced
/// ProjectType has no template rows (the old placeholder categories),
/// falls back to the legacy flat "General" module from TaskTemplates.cs.</summary>
public record CreateProjectRequest(
	int AccountId, string Code, string Name, bool DefaultBillable, int? ProjectTypeId,
	string? ProjectTech = null, string? BillingType = null, string? CustomerPO = null, string? Notes = null,
	int? ProjectLeadEmployeeId = null, int? ProjectManagerEmployeeId = null, int? DeliveryHeadEmployeeId = null);
public record UpdateProjectRequest(
	int? AccountId, string? Code, string? Name, bool? DefaultBillable, bool? IsActive,
	string? ProjectTech = null, string? BillingType = null, string? CustomerPO = null, string? Notes = null,
	int? ProjectLeadEmployeeId = null, int? ProjectManagerEmployeeId = null, int? DeliveryHeadEmployeeId = null);

public record CreateModuleRequest(int ProjectId, string Name, int? ProjectTypeId);
public record UpdateModuleRequest(string? Name, int? ProjectTypeId);

public record CreateTaskRequest(int ModuleId, string Name);
public record UpdateTaskRequest(string? Name);

public record CreateHolidayRequest(DateOnly HolidayDate, string Name, string Location, int? AccountId = null);
public record UpdateHolidayRequest(DateOnly? HolidayDate, string? Name, string? Location, int? AccountId = null);
public record HolidayDto(int HolidayId, DateOnly Date, string Name, string Location, int? AccountId);

// ---- "Others" quick-add (Add Task Line, employee-reachable) ----

/// <summary>Employee picked "Others" for Project and typed a name. Creates a
/// real, immediately-usable Project: placeholder Code, linked to the
/// "Pending Classification" internal Account, flagged NeedsReview so admin
/// can fill in the real Account/Code/BillingType/ProjectType later.</summary>
public record QuickAddProjectRequest(string Name);
/// <summary>Employee picked "Others" for Module. Not part of any Project
/// Type's template - ProjectTypeId stays null on the created Module.</summary>
public record QuickAddModuleRequest(int ProjectId, string Name);
public record QuickAddTaskRequest(int ModuleId, string Name);
