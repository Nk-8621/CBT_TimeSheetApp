using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class MasterDataService(IMasterDataRepository repository) : IMasterDataService
{
	/// <summary>Fixed, T&M, Consumption, NB-ValueAdd, NB-L&D, NB-Training, NB-Travel, Others.</summary>
	private static readonly string[] AllowedBillingTypes =
		["Fixed", "T&M", "Consumption", "NB-ValueAdd", "NB-L&D", "NB-Training", "NB-Travel", "Others"];

	/// <summary>Account new "Others"-created projects roll up to until admin
	/// assigns the real one.</summary>
	private const string PendingClassificationAccountName = "Pending Classification";

	public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default)
	{
		var departments = await repository.GetDepartmentsAsync(ct);
		return departments.Select(d => new DepartmentDto(d.DepartmentId, d.Code, d.Name, d.ParentDepartmentId)).ToList();
	}

	public async Task<IReadOnlyList<LocationDto>> GetLocationsAsync(CancellationToken ct = default)
	{
		var locations = await repository.GetLocationsAsync(ct);
		return locations.Select(l => new LocationDto(l.LocationId, l.Code, l.Name)).ToList();
	}

	public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken ct = default)
	{
		var accounts = await repository.GetAccountsAsync(ct);
		return accounts.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken ct = default)
	{
		var projects = await repository.GetProjectsAsync(ct);
		return projects.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<ModuleDto>> GetModulesAsync(int? projectId = null, CancellationToken ct = default)
	{
		var modules = await repository.GetModulesAsync(projectId, ct);
		return modules.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<WorkTaskDto>> GetTasksAsync(int? moduleId = null, CancellationToken ct = default)
	{
		var tasks = await repository.GetTasksAsync(moduleId, ct);
		return tasks.Select(ToDto).ToList();
	}

	public async Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(CancellationToken ct = default)
	{
		var holidays = await repository.GetHolidaysAsync(ct: ct);
		return holidays.Select(ToDto).ToList();
	}

	// ---- Project Type + templates ----

	public async Task<IReadOnlyList<ProjectTypeDto>> GetProjectTypesAsync(CancellationToken ct = default)
	{
		var types = await repository.GetProjectTypesAsync(ct);
		return types.Select(t => new ProjectTypeDto(t.ProjectTypeId, t.Code, t.Name)).ToList();
	}

	public async Task<IReadOnlyList<ProjectTypeWithTemplateDto>> GetProjectTypesWithTemplatesAsync(CancellationToken ct = default)
	{
		var types = await repository.GetProjectTypesWithTemplatesAsync(ct);
		return types.Select(ToTemplateDto).ToList();
	}

	public async Task<ProjectTypeWithTemplateDto> GetProjectTypeWithTemplateAsync(int projectTypeId, CancellationToken ct = default)
	{
		var type = await repository.GetProjectTypeWithTemplatesByIdAsync(projectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), projectTypeId);
		return ToTemplateDto(type);
	}

	public async Task<ProjectTypeDto> CreateProjectTypeAsync(CreateProjectTypeRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Project Type code and name are both required.");

		var existing = await repository.GetProjectTypesAsync(ct);
		if (existing.Any(t => string.Equals(t.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
			throw new BusinessRuleException($"A Project Type with code \"{request.Code}\" already exists.");

		var projectType = new ProjectType { Code = request.Code.ToUpperInvariant(), Name = request.Name };
		await repository.AddProjectTypeAsync(projectType, ct);
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeDto(projectType.ProjectTypeId, projectType.Code, projectType.Name);
	}

	public async Task<ProjectTypeDto> UpdateProjectTypeAsync(int projectTypeId, UpdateProjectTypeRequest request, CancellationToken ct = default)
	{
		var projectType = await repository.GetProjectTypeByIdAsync(projectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), projectTypeId);

		if (request.Code is not null)
		{
			var existing = await repository.GetProjectTypesAsync(ct);
			if (existing.Any(t => t.ProjectTypeId != projectTypeId && string.Equals(t.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
				throw new BusinessRuleException($"A Project Type with code \"{request.Code}\" already exists.");
			projectType.Code = request.Code.ToUpperInvariant();
		}
		if (request.Name is not null) projectType.Name = request.Name;

		await repository.SaveChangesAsync(ct);
		return new ProjectTypeDto(projectType.ProjectTypeId, projectType.Code, projectType.Name);
	}

	public async Task DeleteProjectTypeAsync(int projectTypeId, DeleteProjectTypeRequest request, CancellationToken ct = default)
	{
		var projectType = await repository.GetProjectTypeByIdAsync(projectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), projectTypeId);

		var referencingProjects = await repository.GetProjectsByProjectTypeIdAsync(projectTypeId, ct);
		var referencingModules = await repository.GetModulesByProjectTypeIdAsync(projectTypeId, ct);

		if (referencingProjects.Count > 0 || referencingModules.Count > 0)
		{
			if (request.ReplacementProjectTypeId is int replacementId)
			{
				if (replacementId == projectTypeId)
					throw new BusinessRuleException("The replacement Project Type must be different from the one being deleted.");
				_ = await repository.GetProjectTypeByIdAsync(replacementId, ct)
					?? throw new EntityNotFoundException(nameof(ProjectType), replacementId);

				foreach (var project in referencingProjects) project.ProjectTypeId = replacementId;
				foreach (var module in referencingModules) module.ProjectTypeId = replacementId;
			}
			else
			{
				throw new BusinessRuleException(
					$"Project Type \"{projectType.Name}\" is still used by {referencingProjects.Count} project(s) and {referencingModules.Count} module(s). " +
					"Supply a ReplacementProjectTypeId to reassign them first.");
			}
		}

		repository.RemoveProjectType(projectType);
		await repository.SaveChangesAsync(ct);
	}

	public async Task<ProjectTypeModuleTemplateDto> CreateModuleTemplateAsync(CreateProjectTypeModuleTemplateRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Module template name is required.");
		_ = await repository.GetProjectTypeByIdAsync(request.ProjectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), request.ProjectTypeId);

		var template = new ProjectTypeModuleTemplate { ProjectTypeId = request.ProjectTypeId, Name = request.Name, SortOrder = request.SortOrder };
		await repository.AddModuleTemplateAsync(template, ct);
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeModuleTemplateDto(template.ProjectTypeModuleTemplateId, template.Name, template.SortOrder, []);
	}

	public async Task<ProjectTypeModuleTemplateDto> UpdateModuleTemplateAsync(int moduleTemplateId, UpdateProjectTypeModuleTemplateRequest request, CancellationToken ct = default)
	{
		var template = await repository.GetModuleTemplateByIdAsync(moduleTemplateId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeModuleTemplate), moduleTemplateId);

		if (request.Name is not null) template.Name = request.Name;
		if (request.SortOrder is int sort) template.SortOrder = sort;
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeModuleTemplateDto(template.ProjectTypeModuleTemplateId, template.Name, template.SortOrder, []);
	}

	public async Task DeleteModuleTemplateAsync(int moduleTemplateId, CancellationToken ct = default)
	{
		var template = await repository.GetModuleTemplateByIdAsync(moduleTemplateId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeModuleTemplate), moduleTemplateId);
		repository.RemoveModuleTemplate(template);
		await repository.SaveChangesAsync(ct);
	}

	public async Task<ProjectTypeTaskTemplateDto> CreateTaskTemplateAsync(CreateProjectTypeTaskTemplateRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Task template name is required.");
		_ = await repository.GetModuleTemplateByIdAsync(request.ProjectTypeModuleTemplateId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeModuleTemplate), request.ProjectTypeModuleTemplateId);

		var template = new ProjectTypeTaskTemplate { ProjectTypeModuleTemplateId = request.ProjectTypeModuleTemplateId, Name = request.Name, SortOrder = request.SortOrder };
		await repository.AddTaskTemplateAsync(template, ct);
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeTaskTemplateDto(template.ProjectTypeTaskTemplateId, template.Name, template.SortOrder);
	}

	public async Task<ProjectTypeTaskTemplateDto> UpdateTaskTemplateAsync(int taskTemplateId, UpdateProjectTypeTaskTemplateRequest request, CancellationToken ct = default)
	{
		var template = await repository.GetTaskTemplateByIdAsync(taskTemplateId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeTaskTemplate), taskTemplateId);

		if (request.Name is not null) template.Name = request.Name;
		if (request.SortOrder is int sort) template.SortOrder = sort;
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeTaskTemplateDto(template.ProjectTypeTaskTemplateId, template.Name, template.SortOrder);
	}

	public async Task DeleteTaskTemplateAsync(int taskTemplateId, CancellationToken ct = default)
	{
		var template = await repository.GetTaskTemplateByIdAsync(taskTemplateId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeTaskTemplate), taskTemplateId);
		repository.RemoveTaskTemplate(template);
		await repository.SaveChangesAsync(ct);
	}

	// ---- Account ----

	public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default)
	{
		var accountType = ParseAccountType(request.AccountType);
		await RequireDepartmentExistsAsync(request.DepartmentId, ct);

		var account = new Account
		{
			Name = request.Name,
			DepartmentId = request.DepartmentId,
			AccountType = accountType,
			CreatedAt = DateTime.UtcNow,
		};
		await repository.AddAccountAsync(account, ct);
		await repository.SaveChangesAsync(ct);
		return ToDto(account);
	}

	public async Task<AccountDto> UpdateAccountAsync(int accountId, UpdateAccountRequest request, CancellationToken ct = default)
	{
		var account = await repository.GetAccountByIdAsync(accountId, ct)
			?? throw new EntityNotFoundException(nameof(Account), accountId);

		if (request.Name is not null) account.Name = request.Name;
		if (request.DepartmentId is int deptId) { await RequireDepartmentExistsAsync(deptId, ct); account.DepartmentId = deptId; }
		if (request.AccountType is not null) account.AccountType = ParseAccountType(request.AccountType);
		account.UpdatedAt = DateTime.UtcNow;

		await repository.SaveChangesAsync(ct);
		return ToDto(account);
	}

	// ---- Project ----

	public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
			throw new BusinessRuleException("Project name and code are both required.");
		RequireValidBillingType(request.BillingType);
		RequireValidDefaultBillable(request.DefaultBillable);

		var existingProjects = await repository.GetProjectsAsync(ct);
		if (existingProjects.Any(p => string.Equals(p.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
			throw new BusinessRuleException($"A project with code \"{request.Code}\" already exists.");

		_ = await repository.GetAccountByIdAsync(request.AccountId, ct)
			?? throw new EntityNotFoundException(nameof(Account), request.AccountId);

		ProjectType? projectType = null;
		if (request.ProjectTypeId is int typeId)
			projectType = await repository.GetProjectTypeWithTemplatesByIdAsync(typeId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), typeId);

		var project = new Project
		{
			Name = request.Name,
			Code = request.Code.ToUpperInvariant(),
			AccountId = request.AccountId,
			DefaultBillable = request.DefaultBillable,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
			ProjectTypeId = request.ProjectTypeId,
			ProjectTech = request.ProjectTech,
			BillingType = request.BillingType,
			CustomerPO = request.CustomerPO,
			Notes = request.Notes,
			ProjectLeadEmployeeId = request.ProjectLeadEmployeeId,
			ProjectManagerEmployeeId = request.ProjectManagerEmployeeId,
			DeliveryHeadEmployeeId = request.DeliveryHeadEmployeeId,
		};
		await repository.AddProjectAsync(project, ct);
		await repository.SaveChangesAsync(ct); // populates project.ProjectId before a module can reference it

		// A brand-new project has zero existing Modules, so "merge the
		// template in" and "generate from the template" are the same
		// operation - no need for a separate generate-only code path.
		if (projectType is not null)
		{
			await MergeModulesFromTemplateAsync(project, projectType, ct);
			await repository.SaveChangesAsync(ct);
		}

		var created = await repository.GetProjectByIdAsync(project.ProjectId, ct) ?? project;
		return ToDto(created);
	}

	public async Task<ProjectDto> UpdateProjectAsync(int projectId, UpdateProjectRequest request, CancellationToken ct = default)
	{
		var project = await repository.GetProjectByIdAsync(projectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), projectId);

		if (request.AccountId is int accId)
		{
			_ = await repository.GetAccountByIdAsync(accId, ct) ?? throw new EntityNotFoundException(nameof(Account), accId);
			project.AccountId = accId;
		}
		if (request.Name is not null) project.Name = request.Name;
		if (request.Code is not null)
		{
			var existing = await repository.GetProjectsAsync(ct);
			if (existing.Any(p => p.ProjectId != projectId && string.Equals(p.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
				throw new BusinessRuleException($"A project with code \"{request.Code}\" already exists.");
			project.Code = request.Code.ToUpperInvariant();
		}
		if (request.DefaultBillable is string defaultBillable)
		{
			RequireValidDefaultBillable(defaultBillable);
			project.DefaultBillable = defaultBillable;
		}
		if (request.IsActive is bool active) project.IsActive = active;
		if (request.ProjectTech is not null) project.ProjectTech = request.ProjectTech;
		if (request.BillingType is not null)
		{
			RequireValidBillingType(request.BillingType);
			project.BillingType = request.BillingType;
		}
		if (request.CustomerPO is not null) project.CustomerPO = request.CustomerPO;
		if (request.Notes is not null) project.Notes = request.Notes;
		if (request.NeedsReview is bool needsReview) project.NeedsReview = needsReview;
		if (request.ProjectLeadEmployeeId is int leadId) project.ProjectLeadEmployeeId = leadId;
		if (request.ProjectManagerEmployeeId is int mgrId) project.ProjectManagerEmployeeId = mgrId;
		if (request.DeliveryHeadEmployeeId is int dhId) project.DeliveryHeadEmployeeId = dhId;
		project.UpdatedAt = DateTime.UtcNow;

		// Retroactive classification: ONLY takes effect the moment a project
		// that never had a Project Type gets one (an older project, or one
		// created without one). Once set, this can never change it again -
		// the Module/Task template has already been applied and shouldn't
		// silently change out from under an in-flight project.
		if (request.ProjectTypeId is int newTypeId && project.ProjectTypeId is null)
		{
			var projectType = await repository.GetProjectTypeWithTemplatesByIdAsync(newTypeId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), newTypeId);
			project.ProjectTypeId = newTypeId;
			await MergeModulesFromTemplateAsync(project, projectType, ct);
		}

		await repository.SaveChangesAsync(ct);

		var updated = await repository.GetProjectByIdAsync(projectId, ct) ?? project;
		return ToDto(updated);
	}

	/// <summary>Explicit re-sync for a project that already has a Project
	/// Type - see IMasterDataService's summary. Same merge-only rule as the
	/// automatic sync in UpdateProjectAsync, just callable again later
	/// instead of only at the moment a type is first assigned.</summary>
	public async Task<ProjectDto> SyncProjectModulesFromTemplateAsync(int projectId, CancellationToken ct = default)
	{
		var project = await repository.GetProjectByIdAsync(projectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), projectId);

		if (project.ProjectTypeId is not int typeId)
			throw new BusinessRuleException("This project has no Project Type set yet - nothing to sync from.");

		var projectType = await repository.GetProjectTypeWithTemplatesByIdAsync(typeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), typeId);

		await MergeModulesFromTemplateAsync(project, projectType, ct);
		await repository.SaveChangesAsync(ct);

		var updated = await repository.GetProjectByIdAsync(projectId, ct) ?? project;
		return ToDto(updated);
	}

	// ---- Module ----

	public async Task<ModuleDto> CreateModuleAsync(CreateModuleRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Module name is required.");

		_ = await repository.GetProjectByIdAsync(request.ProjectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), request.ProjectId);

		ProjectType? projectType = null;
		if (request.ProjectTypeId is int typeId)
			projectType = await repository.GetProjectTypeByIdAsync(typeId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), typeId);

		var module = new Module { Name = request.Name, ProjectId = request.ProjectId, ProjectTypeId = request.ProjectTypeId, CreatedAt = DateTime.UtcNow };
		await repository.AddModuleAsync(module, ct);
		await repository.SaveChangesAsync(ct);
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, module.ProjectTypeId, projectType?.Code);
	}

	public async Task<ModuleDto> UpdateModuleAsync(int moduleId, UpdateModuleRequest request, CancellationToken ct = default)
	{
		var module = await repository.GetModuleByIdAsync(moduleId, ct)
			?? throw new EntityNotFoundException(nameof(Module), moduleId);

		if (request.Name is not null) module.Name = request.Name;

		var projectType = module.ProjectType;
		if (request.ProjectTypeId is int typeId)
		{
			projectType = await repository.GetProjectTypeByIdAsync(typeId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), typeId);
			module.ProjectTypeId = typeId;
		}

		await repository.SaveChangesAsync(ct);
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, module.ProjectTypeId, projectType?.Code);
	}

	// ---- Task ----

	public async Task<WorkTaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Task name is required.");

		_ = await repository.GetModuleByIdAsync(request.ModuleId, ct)
			?? throw new EntityNotFoundException(nameof(Module), request.ModuleId);

		var task = new WorkTask { Name = request.Name, ModuleId = request.ModuleId, CreatedAt = DateTime.UtcNow };
		await repository.AddTaskAsync(task, ct);
		await repository.SaveChangesAsync(ct);
		return ToDto(task);
	}

	public async Task<WorkTaskDto> UpdateTaskAsync(int taskId, UpdateTaskRequest request, CancellationToken ct = default)
	{
		var task = await repository.GetTaskByIdAsync(taskId, ct)
			?? throw new EntityNotFoundException(nameof(WorkTask), taskId);

		if (request.Name is not null) task.Name = request.Name;
		await repository.SaveChangesAsync(ct);
		return ToDto(task);
	}

	// ---- Quick add ("Others" self-service, from the timesheet entry screen) ----

	public async Task<ProjectDto> QuickAddProjectAsync(QuickAddProjectRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Project name is required.");

		var pendingAccount = await GetOrCreatePendingClassificationAccountAsync(ct);
		var code = await GenerateNextPendingProjectCodeAsync(ct);

		var project = new Project
		{
			Name = request.Name,
			Code = code,
			AccountId = pendingAccount.AccountId,
			DefaultBillable = "NonBillable",
			IsActive = true,
			NeedsReview = true,
			CreatedAt = DateTime.UtcNow,
		};
		await repository.AddProjectAsync(project, ct);
		await repository.SaveChangesAsync(ct);

		var created = await repository.GetProjectByIdAsync(project.ProjectId, ct) ?? project;
		return ToDto(created);
	}

	public async Task<ModuleDto> QuickAddModuleAsync(QuickAddModuleRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Module name is required.");
		_ = await repository.GetProjectByIdAsync(request.ProjectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), request.ProjectId);

		var module = new Module { Name = request.Name, ProjectId = request.ProjectId, CreatedAt = DateTime.UtcNow };
		await repository.AddModuleAsync(module, ct);
		await repository.SaveChangesAsync(ct);
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, null, null);
	}

	public async Task<WorkTaskDto> QuickAddTaskAsync(QuickAddTaskRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Task name is required.");
		_ = await repository.GetModuleByIdAsync(request.ModuleId, ct)
			?? throw new EntityNotFoundException(nameof(Module), request.ModuleId);

		var task = new WorkTask { Name = request.Name, ModuleId = request.ModuleId, CreatedAt = DateTime.UtcNow };
		await repository.AddTaskAsync(task, ct);
		await repository.SaveChangesAsync(ct);
		return ToDto(task);
	}

	// ---- Holiday ----

	public async Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location))
			throw new BusinessRuleException("Holiday name and location are both required.");

		if (request.AccountId is int accId)
			_ = await repository.GetAccountByIdAsync(accId, ct) ?? throw new EntityNotFoundException(nameof(Account), accId);

		var holiday = new Holiday
		{
			HolidayDate = request.HolidayDate,
			Name = request.Name,
			Location = request.Location,
			AccountId = request.AccountId,
			SourceSystem = "Manual",
			SyncedAt = DateTime.UtcNow,
		};

		await repository.AddHolidayAsync(holiday, ct);
		await repository.SaveChangesAsync(ct);
		return ToDto(holiday);
	}

	public async Task<HolidayDto> UpdateHolidayAsync(int holidayId, UpdateHolidayRequest request, CancellationToken ct = default)
	{
		var holiday = await repository.GetHolidayByIdAsync(holidayId, ct)
			?? throw new EntityNotFoundException(nameof(Holiday), holidayId);

		if (request.HolidayDate is DateOnly date) holiday.HolidayDate = date;
		if (request.Name is not null) holiday.Name = request.Name;
		if (request.Location is not null) holiday.Location = request.Location;

		if (request.AccountId is int accId)
		{
			_ = await repository.GetAccountByIdAsync(accId, ct) ?? throw new EntityNotFoundException(nameof(Account), accId);
			holiday.AccountId = accId;
		}
		holiday.SourceSystem = "Manual"; // no longer purely KEKA-sourced once hand-edited
		holiday.SyncedAt = DateTime.UtcNow;

		await repository.SaveChangesAsync(ct);
		return ToDto(holiday);
	}

	public async Task DeleteHolidayAsync(int holidayId, CancellationToken ct = default)
	{
		var holiday = await repository.GetHolidayByIdAsync(holidayId, ct)
			?? throw new EntityNotFoundException(nameof(Holiday), holidayId);
		repository.RemoveHoliday(holiday);
		await repository.SaveChangesAsync(ct);
	}

	// ---- Project-wise resource allocation (admin reporting) ----

	public async Task<IReadOnlyList<ProjectResourceAllocationDto>> GetProjectResourceAllocationsAsync(CancellationToken ct = default)
	{
		var projects = await repository.GetProjectsWithAllocationsAsync(ct);
		return projects
			.Select(p => new ProjectResourceAllocationDto(p.ProjectId, p.Code, p.Name, p.EmployeeAllocations.Count))
			.OrderByDescending(r => r.ResourceCount)
			.ToList();
	}

	public async Task<IReadOnlyList<AllocatedEmployeeDto>> GetAllocatedEmployeesAsync(int projectId, CancellationToken ct = default)
	{
		var project = await repository.GetProjectWithAllocationsByIdAsync(projectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), projectId);

		return project.EmployeeAllocations
			.Where(a => a.Employee is not null)
			.Select(a => new AllocatedEmployeeDto(a.Employee!.EmployeeId, a.Employee.EmployeeCode, a.Employee.FullName, a.Employee.Department?.Name ?? "—"))
			.OrderBy(e => e.FullName)
			.ToList();
	}

	// ---- Shared helpers ----

	/// <summary>Adds whichever of the Project Type's template Modules/Tasks
	/// the Project is still missing, matched by name (case-insensitive) -
	/// never renames, removes, or otherwise touches anything that already
	/// exists. For a brand-new Project (zero existing Modules) this is
	/// equivalent to generating the whole tree from scratch; for an
	/// already-populated Project it's a safe top-up.</summary>
	private async Task MergeModulesFromTemplateAsync(Project project, ProjectType projectType, CancellationToken ct)
	{
		var existingModules = await repository.GetModulesAsync(project.ProjectId, ct);
		foreach (var moduleTemplate in projectType.ModuleTemplates.OrderBy(m => m.SortOrder))
		{
			var existingModule = existingModules.FirstOrDefault(m => string.Equals(m.Name, moduleTemplate.Name, StringComparison.OrdinalIgnoreCase));
			int moduleId;
			if (existingModule is null)
			{
				var newModule = new Module { ProjectId = project.ProjectId, ProjectTypeId = projectType.ProjectTypeId, Name = moduleTemplate.Name, CreatedAt = DateTime.UtcNow };
				await repository.AddModuleAsync(newModule, ct);
				await repository.SaveChangesAsync(ct); // populates newModule.ModuleId before a task can reference it
				moduleId = newModule.ModuleId;
			}
			else
			{
				moduleId = existingModule.ModuleId;
			}

			var existingTasks = await repository.GetTasksAsync(moduleId, ct);
			foreach (var taskTemplate in moduleTemplate.TaskTemplates.OrderBy(t => t.SortOrder))
			{
				if (existingTasks.Any(t => string.Equals(t.Name, taskTemplate.Name, StringComparison.OrdinalIgnoreCase))) continue;
				await repository.AddTaskAsync(new WorkTask { ModuleId = moduleId, Name = taskTemplate.Name, CreatedAt = DateTime.UtcNow }, ct);
			}
		}
	}

	private async Task<Account> GetOrCreatePendingClassificationAccountAsync(CancellationToken ct)
	{
		var existing = await repository.GetAccountByNameAsync(PendingClassificationAccountName, ct);
		if (existing is not null) return existing;

		// Any Department works as a home for this account - it's an
		// administrative bucket, not tied to a real department. Pick the
		// lowest DepartmentId deterministically so this is stable across calls.
		var departments = await repository.GetDepartmentsAsync(ct);
		var department = departments.OrderBy(d => d.DepartmentId).FirstOrDefault()
			?? throw new BusinessRuleException("Cannot create the \"Pending Classification\" account - no departments exist yet.");

		var account = new Account
		{
			Name = PendingClassificationAccountName,
			DepartmentId = department.DepartmentId,
			AccountType = AccountType.Internal,
			CreatedAt = DateTime.UtcNow,
		};
		await repository.AddAccountAsync(account, ct);
		await repository.SaveChangesAsync(ct);
		return account;
	}

	/// <summary>Placeholder codes look like PEND-1, PEND-2, ... - admin
	/// replaces them with a real code once the project is properly classified.</summary>
	private async Task<string> GenerateNextPendingProjectCodeAsync(CancellationToken ct)
	{
		var projects = await repository.GetProjectsAsync(ct);
		var maxSuffix = projects
			.Select(p => p.Code)
			.Where(c => c.StartsWith("PEND-", StringComparison.OrdinalIgnoreCase))
			.Select(c => int.TryParse(c.AsSpan(5), out var n) ? n : 0)
			.DefaultIfEmpty(0)
			.Max();
		return $"PEND-{maxSuffix + 1}";
	}

	private async Task RequireDepartmentExistsAsync(int departmentId, CancellationToken ct)
	{
		var departments = await repository.GetDepartmentsAsync(ct);
		if (!departments.Any(d => d.DepartmentId == departmentId))
			throw new EntityNotFoundException(nameof(Department), departmentId);
	}

	private static void RequireValidBillingType(string? billingType)
	{
		if (!string.IsNullOrWhiteSpace(billingType) && !AllowedBillingTypes.Contains(billingType, StringComparer.OrdinalIgnoreCase))
			throw new BusinessRuleException($"Billing type must be one of: {string.Join(", ", AllowedBillingTypes)} (got \"{billingType}\").");
	}

	/// <summary>Same vocabulary as TimeEntry.Classification (Billable /
	/// NonBillable / PartialBillable) - see BillingClassificationRules. This
	/// only sets what new task lines on the project default to; it never
	/// touches any line's own Classification once created.</summary>
	private static void RequireValidDefaultBillable(string defaultBillable)
	{
		if (!BillingClassificationRules.Classifications.Contains(defaultBillable, StringComparer.OrdinalIgnoreCase))
			throw new BusinessRuleException($"Default classification must be one of: {string.Join(", ", BillingClassificationRules.Classifications)} (got \"{defaultBillable}\").");
	}

	private static AccountType ParseAccountType(string value) =>
		Enum.TryParse<AccountType>(value, out var parsed)
			? parsed
			: throw new BusinessRuleException($"Account type must be \"Customer\" or \"Internal\" (got \"{value}\").");

	private static ProjectTypeWithTemplateDto ToTemplateDto(ProjectType t) => new(
		t.ProjectTypeId, t.Code, t.Name,
		t.ModuleTemplates.OrderBy(m => m.SortOrder).Select(m => new ProjectTypeModuleTemplateDto(
			m.ProjectTypeModuleTemplateId, m.Name, m.SortOrder,
			m.TaskTemplates.OrderBy(x => x.SortOrder).Select(x => new ProjectTypeTaskTemplateDto(x.ProjectTypeTaskTemplateId, x.Name, x.SortOrder)).ToList()
		)).ToList());

	private static AccountDto ToDto(Account a) => new(a.AccountId, a.DepartmentId, a.Name, a.AccountType.ToString());

	private static ProjectDto ToDto(Project p) => new(
		p.ProjectId, p.AccountId, p.Code, p.Name, p.DefaultBillable, p.IsActive,
		p.ProjectTypeId, p.ProjectType?.Name, p.ProjectTech, p.BillingType,
		p.CustomerPO, p.Notes, p.NeedsReview,
		p.ProjectLeadEmployeeId, p.ProjectLeadEmployee?.FullName,
		p.ProjectManagerEmployeeId, p.ProjectManagerEmployee?.FullName,
		p.DeliveryHeadEmployeeId, p.DeliveryHeadEmployee?.FullName);

	private static ModuleDto ToDto(Module m) => new(m.ModuleId, m.ProjectId, m.Name, m.ProjectTypeId, m.ProjectType?.Code);
	private static WorkTaskDto ToDto(WorkTask t) => new(t.TaskId, t.ModuleId, t.Name);
	private static HolidayDto ToDto(Holiday h) => new(h.HolidayId, h.HolidayDate, h.Name, h.Location, h.AccountId);
}
