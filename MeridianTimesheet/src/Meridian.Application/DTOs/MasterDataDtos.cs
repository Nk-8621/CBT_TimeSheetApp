namespace Meridian.Application.DTOs;

public record DepartmentDto(int Id, string Code, string Name, int? ParentDepartmentId);
public record LocationDto(int Id, string Code, string Name);
public record AccountDto(int Id, int DepartmentId, string Name, string AccountType);

public record ProjectDto(
	int Id,
	int AccountId,
	string Code,
	string Name,
	bool DefaultBillable,
	bool IsActive,
	int? ProjectTypeId,
	string? ProjectTypeName,
	string? ProjectTech,
	string? BillingType,
	string? CustomerPO,
	string? Notes,
	bool NeedsReview,
	int? ProjectLeadEmployeeId,
	string? ProjectLeadEmployeeName,
	int? ProjectManagerEmployeeId,
	string? ProjectManagerEmployeeName,
	int? DeliveryHeadEmployeeId,
	string? DeliveryHeadEmployeeName
);

public record ModuleDto(int Id, int ProjectId, string Name, int? ProjectTypeId, string? ProjectTypeCode);
public record WorkTaskDto(int Id, int ModuleId, string Name);
//public record HolidayDto(int Id, DateOnly Date, string Name, string Location);

public record ProjectTypeDto(int Id, string Code, string Name);
public record ProjectTypeTaskTemplateDto(int Id, int ProjectTypeModuleTemplateId, string Name, int SortOrder);
public record ProjectTypeModuleTemplateDto(int Id, int ProjectTypeId, string Name, int SortOrder);

/// <summary>A Project Type together with its full Level-1/Level-2 template
/// tree - backs the Project Type template management screen.</summary>
public record ProjectTypeWithTemplateDto(int Id, string Code, string Name, IReadOnlyList<ModuleWithTaskTemplatesDto> Modules);
public record ModuleWithTaskTemplatesDto(int Id, string Name, int SortOrder, IReadOnlyList<ProjectTypeTaskTemplateDto> Tasks);

// ---- Admin-only create/update requests (Section: Master Data CRUD) ----

public record CreateAccountRequest(int DepartmentId, string Name, string AccountType);
public record UpdateAccountRequest(int? DepartmentId, string? Name, string? AccountType);

/// <summary>ProjectTypeId, if supplied, auto-creates the project's starter
/// Modules/Tasks from that Project Type's template - matching the original
/// wireframe's behavior of a new project being immediately usable with no
/// second setup step. Pass null to create an empty project with no modules
/// yet.</summary>
public record CreateProjectRequest(
	int AccountId,
	string Code,
	string Name,
	bool DefaultBillable,
	int? ProjectTypeId,
	string? ProjectTech = null,
	string? BillingType = null,
	string? CustomerPO = null,
	string? Notes = null,
	int? ProjectLeadEmployeeId = null,
	int? ProjectManagerEmployeeId = null,
	int? DeliveryHeadEmployeeId = null
);

/// <summary>ProjectTypeId here is retroactive-classification-only: it is
/// applied ONLY when the project currently has no ProjectTypeId (a project
/// created before this feature existed, or created without one). It does
/// NOT auto-create that Project Type's starter Modules/Tasks the way
/// CreateProjectRequest.ProjectTypeId does - it just records the
/// classification against whatever Modules/Tasks the project already has.
/// Once a project has a ProjectTypeId, this request can no longer change it
/// (its Module/Task template has already been applied and shouldn't
/// silently change) - the service ignores ProjectTypeId in that case.</summary>
public record UpdateProjectRequest(
	int? AccountId,
	string? Code,
	string? Name,
	bool? DefaultBillable,
	bool? IsActive,
	int? ProjectTypeId = null,
	string? ProjectTech = null,
	string? BillingType = null,
	string? CustomerPO = null,
	string? Notes = null,
	bool? NeedsReview = null,
	int? ProjectLeadEmployeeId = null,
	int? ProjectManagerEmployeeId = null,
	int? DeliveryHeadEmployeeId = null
);

public record CreateModuleRequest(int ProjectId, string Name, int? ProjectTypeId);
public record UpdateModuleRequest(string? Name, int? ProjectTypeId);

public record CreateTaskRequest(int ModuleId, string Name);
public record UpdateTaskRequest(string? Name);

public record CreateProjectTypeRequest(string Code, string Name);
public record UpdateProjectTypeRequest(string? Code, string? Name);
/// <summary>ReplacementProjectTypeId lets admin reassign every Project/Module
/// currently on this Project Type to another one before it's deleted -
/// omit/null to just attempt the delete (which fails with a clear error if
/// anything still references it).</summary>
public record DeleteProjectTypeRequest(int? ReplacementProjectTypeId);

public record CreateProjectTypeModuleTemplateRequest(int ProjectTypeId, string Name, int SortOrder);
public record UpdateProjectTypeModuleTemplateRequest(string? Name);
public record CreateProjectTypeTaskTemplateRequest(int ProjectTypeModuleTemplateId, string Name, int SortOrder);
public record UpdateProjectTypeTaskTemplateRequest(string? Name);

/// <summary>Self-service "Others" quick-add from the timesheet entry screen -
/// deliberately minimal (no account/type picking); the project lands in the
/// "Pending Classification" internal account, flagged NeedsReview, for an
/// admin to properly classify later.</summary>
public record QuickAddProjectRequest(string Name);
public record QuickAddModuleRequest(int ProjectId, string Name);
public record QuickAddTaskRequest(int ModuleId, string Name);

public record CreateHolidayRequest(DateOnly HolidayDate, string Name, string Location, int? AccountId = null);
public record UpdateHolidayRequest(DateOnly? HolidayDate, string? Name, string? Location, int? AccountId = null);
public record HolidayDto(int HolidayId, DateOnly Date, string Name, string Location, int? AccountId);

// ---- Project-wise resource allocation (admin reporting) ----

public record ProjectResourceAllocationDto(int ProjectId, string ProjectCode, string ProjectName, int ResourceCount);
public record AllocatedEmployeeDto(int EmployeeId, string EmployeeCode, string FullName, string DepartmentName);
public record SetEmployeeProjectAllocationsRequest(IReadOnlyList<int> ProjectIds);
