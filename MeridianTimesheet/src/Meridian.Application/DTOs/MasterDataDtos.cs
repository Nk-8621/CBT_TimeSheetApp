namespace Meridian.Application.DTOs;

public record DepartmentDto(int Id, string Code, string Name, int? ParentDepartmentId);
public record LocationDto(int Id, string Code, string Name);
public record AccountDto(int Id, int DepartmentId, string Name, string AccountType);

public record ProjectDto(
	int Id, int AccountId, string Code, string Name, string DefaultBillable, bool IsActive,
	int? ProjectTypeId, string? ProjectTypeName, string? ProjectTech, string? BillingType,
	string? CustomerPO, string? Notes, bool NeedsReview,
	int? ProjectLeadEmployeeId, string? ProjectLeadEmployeeName,
	int? ProjectManagerEmployeeId, string? ProjectManagerEmployeeName,
	int? DeliveryHeadEmployeeId, string? DeliveryHeadEmployeeName);

public record ModuleDto(int Id, int ProjectId, string Name, int? ProjectTypeId, string? ProjectTypeCode);
public record WorkTaskDto(int Id, int ModuleId, string Name);

// ---- Project Type + its Level-1/Level-2 template ----

public record ProjectTypeDto(int Id, string Code, string Name);
public record ProjectTypeTaskTemplateDto(int Id, string Name, int SortOrder);
public record ProjectTypeModuleTemplateDto(int Id, string Name, int SortOrder, IReadOnlyList<ProjectTypeTaskTemplateDto> Tasks);
/// <summary>Full tree for the admin Project Type template-management screen.</summary>
public record ProjectTypeWithTemplateDto(int Id, string Code, string Name, IReadOnlyList<ProjectTypeModuleTemplateDto> Modules);

public record CreateProjectTypeRequest(string Code, string Name);
public record UpdateProjectTypeRequest(string? Code, string? Name);
/// <summary>Deleting a Project Type that any Project/Module still uses
/// requires picking a replacement up front - every affected Project/Module
/// gets reassigned to ReplacementProjectTypeId before the delete proceeds,
/// so nothing is ever left orphaned.</summary>
public record DeleteProjectTypeRequest(int? ReplacementProjectTypeId);

public record CreateProjectTypeModuleTemplateRequest(int ProjectTypeId, string Name, int SortOrder);
public record UpdateProjectTypeModuleTemplateRequest(string? Name, int? SortOrder);
public record CreateProjectTypeTaskTemplateRequest(int ProjectTypeModuleTemplateId, string Name, int SortOrder);
public record UpdateProjectTypeTaskTemplateRequest(string? Name, int? SortOrder);

// ---- Admin-only create/update requests (Master Data screen) ----

public record CreateAccountRequest(int DepartmentId, string Name, string AccountType);
public record UpdateAccountRequest(int? DepartmentId, string? Name, string? AccountType);

/// <summary>ProjectTypeId, if supplied, auto-generates the full real Module
/// (Level-1) / Task (Level-2) tree from that type's template under the new
/// project. Pass null/omit to create an empty project with no modules yet.</summary>
public record CreateProjectRequest(
	int AccountId, string Code, string Name, string DefaultBillable, int? ProjectTypeId,
	string? ProjectTech = null, string? BillingType = null, string? CustomerPO = null, string? Notes = null,
	int? ProjectLeadEmployeeId = null, int? ProjectManagerEmployeeId = null, int? DeliveryHeadEmployeeId = null);

/// <summary>ProjectTypeId is retroactive-classification-only - the backend
/// applies it ONLY when the project currently has no ProjectTypeId (one
/// created before this feature, or without one). The moment it's applied,
/// that type's Modules/Tasks are merged in (see UpdateProjectAsync /
/// SyncProjectModulesFromTemplateAsync). Once a project has a ProjectTypeId
/// this can no longer change it - the backend silently ignores it then.</summary>
public record UpdateProjectRequest(
	int? AccountId, string? Code, string? Name, string? DefaultBillable, bool? IsActive,
	int? ProjectTypeId = null,
	string? ProjectTech = null, string? BillingType = null, string? CustomerPO = null, string? Notes = null,
	bool? NeedsReview = null,
	int? ProjectLeadEmployeeId = null, int? ProjectManagerEmployeeId = null, int? DeliveryHeadEmployeeId = null);

public record CreateModuleRequest(int ProjectId, string Name, int? ProjectTypeId = null);
public record UpdateModuleRequest(string? Name, int? ProjectTypeId = null);

public record CreateTaskRequest(int ModuleId, string Name);
public record UpdateTaskRequest(string? Name);

/// <summary>Self-service "Others" quick-add from the timesheet entry screen
/// (Add Task Line) - reachable by any authenticated employee, not just
/// admins. Deliberately minimal (no account/type picking); the created
/// Project lands in the "Pending Classification" internal account, flagged
/// NeedsReview, for an admin to properly classify later.</summary>
public record QuickAddProjectRequest(string Name);
/// <summary>Not part of any Project Type's template - ProjectTypeId stays
/// null on the created Module.</summary>
public record QuickAddModuleRequest(int ProjectId, string Name);
public record QuickAddTaskRequest(int ModuleId, string Name);

public record CreateHolidayRequest(DateOnly HolidayDate, string Name, string Location, int? AccountId = null);
public record UpdateHolidayRequest(DateOnly? HolidayDate, string? Name, string? Location, int? AccountId = null);
public record HolidayDto(int HolidayId, DateOnly Date, string Name, string Location, int? AccountId);

// ---- Project-wise resource allocation (admin reporting) ----

public record ProjectResourceAllocationDto(int ProjectId, string ProjectCode, string ProjectName, int ResourceCount);
public record AllocatedEmployeeDto(int EmployeeId, string EmployeeCode, string FullName, string DepartmentName);
