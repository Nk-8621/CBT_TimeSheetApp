using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class MasterDataService(IMasterDataRepository repository, IEmployeeRepository employeeRepository) : IMasterDataService
{
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

	public async Task<ProjectTypeWithTemplateDto> GetProjectTypeWithTemplateAsync(int projectTypeId, CancellationToken ct = default)
	{
		var type = await repository.GetProjectTypeWithTemplatesByIdAsync(projectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), projectTypeId);

		var modules = type.ModuleTemplates
			.OrderBy(m => m.SortOrder)
			.Select(m => new ModuleWithTaskTemplatesDto(
				m.ProjectTypeModuleTemplateId,
				m.Name,
				m.SortOrder,
				m.TaskTemplates.OrderBy(t => t.SortOrder)
					.Select(t => new ProjectTypeTaskTemplateDto(t.ProjectTypeTaskTemplateId, t.ProjectTypeModuleTemplateId, t.Name, t.SortOrder))
					.ToList()))
			.ToList();

		return new ProjectTypeWithTemplateDto(type.ProjectTypeId, type.Code, type.Name, modules);
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
		return new ProjectTypeModuleTemplateDto(template.ProjectTypeModuleTemplateId, template.ProjectTypeId, template.Name, template.SortOrder);
	}

	public async Task<ProjectTypeModuleTemplateDto> UpdateModuleTemplateAsync(int moduleTemplateId, UpdateProjectTypeModuleTemplateRequest request, CancellationToken ct = default)
	{
		var template = await repository.GetModuleTemplateByIdAsync(moduleTemplateId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeModuleTemplate), moduleTemplateId);

		if (request.Name is not null) template.Name = request.Name;
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeModuleTemplateDto(template.ProjectTypeModuleTemplateId, template.ProjectTypeId, template.Name, template.SortOrder);
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
		return new ProjectTypeTaskTemplateDto(template.ProjectTypeTaskTemplateId, template.ProjectTypeModuleTemplateId, template.Name, template.SortOrder);
	}

	public async Task<ProjectTypeTaskTemplateDto> UpdateTaskTemplateAsync(int taskTemplateId, UpdateProjectTypeTaskTemplateRequest request, CancellationToken ct = default)
	{
		var template = await repository.GetTaskTemplateByIdAsync(taskTemplateId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeTaskTemplate), taskTemplateId);

		if (request.Name is not null) template.Name = request.Name;
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeTaskTemplateDto(template.ProjectTypeTaskTemplateId, template.ProjectTypeModuleTemplateId, template.Name, template.SortOrder);
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

		var existingProjects = await repository.GetProjectsAsync(ct);
		if (existingProjects.Any(p => string.Equals(p.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
			throw new BusinessRuleException($"A project with code \"{request.Code}\" already exists.");

		_ = await repository.GetAccountByIdAsync(request.AccountId, ct)
			?? throw new EntityNotFoundException(nameof(Account), request.AccountId);

		ProjectType? projectType = null;
		if (request.ProjectTypeId is int typeId)
			projectType = await repository.GetProjectTypeWithTemplatesByIdAsync(typeId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), typeId);

		await ValidateLeadershipEmployeesAsync(request.ProjectLeadEmployeeId, request.ProjectManagerEmployeeId, request.DeliveryHeadEmployeeId, ct);

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

		// Matching the original wireframe: a Project Type's full Module/Task
		// template tree is applied immediately, so the project is usable on
		// the grid with no second setup step.
		if (projectType is not null)
		{
			foreach (var moduleTemplate in projectType.ModuleTemplates.OrderBy(m => m.SortOrder))
			{
				var module = new Module
				{
					ProjectId = project.ProjectId,
					ProjectTypeId = projectType.ProjectTypeId,
					Name = moduleTemplate.Name,
					CreatedAt = DateTime.UtcNow,
				};
				await repository.AddModuleAsync(module, ct);
				await repository.SaveChangesAsync(ct); // populates module.ModuleId before a task can reference it

				foreach (var taskTemplate in moduleTemplate.TaskTemplates.OrderBy(t => t.SortOrder))
					await repository.AddTaskAsync(new WorkTask { ModuleId = module.ModuleId, Name = taskTemplate.Name, CreatedAt = DateTime.UtcNow }, ct);
			}
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
		if (request.DefaultBillable is bool billable) project.DefaultBillable = billable;
		if (request.IsActive is bool active) project.IsActive = active;

		// Retroactive classification - see UpdateProjectRequest's summary. A
		// project that already has a ProjectTypeId keeps it; this never
		// changes an existing classification.
		if (request.ProjectTypeId is int newTypeId && project.ProjectTypeId is null)
		{
			var projectType = await repository.GetProjectTypeWithTemplatesByIdAsync(newTypeId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), newTypeId);
			project.ProjectTypeId = newTypeId;

			await MergeModulesFromTemplateAsync(project, projectType, ct);
		}

		if (request.ProjectTech is not null) project.ProjectTech = request.ProjectTech;
		if (request.BillingType is not null) project.BillingType = request.BillingType;
		if (request.CustomerPO is not null) project.CustomerPO = request.CustomerPO;
		if (request.Notes is not null) project.Notes = request.Notes;
		if (request.NeedsReview is bool needsReview) project.NeedsReview = needsReview;

		await ValidateLeadershipEmployeesAsync(request.ProjectLeadEmployeeId, request.ProjectManagerEmployeeId, request.DeliveryHeadEmployeeId, ct);
		if (request.ProjectLeadEmployeeId is int leadId) project.ProjectLeadEmployeeId = leadId;
		if (request.ProjectManagerEmployeeId is int mgrId) project.ProjectManagerEmployeeId = mgrId;
		if (request.DeliveryHeadEmployeeId is int dhId) project.DeliveryHeadEmployeeId = dhId;

		project.UpdatedAt = DateTime.UtcNow;
		await repository.SaveChangesAsync(ct);

		// Re-fetch so a ProjectTypeId set just above (or any other
		// navigation-backed field) reflects correctly in the returned DTO -
		// same reasoning as CreateProjectAsync below.
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

	/// <summary>Merge-only sync: adds whichever of the type's template
	/// Modules/Tasks the project doesn't already have (matched by name,
	/// case-insensitive). Never renames or removes anything already on the
	/// project, so existing Modules/Tasks - and any logged time against
	/// them - are left exactly as they were. Caller is responsible for the
	/// final SaveChangesAsync (this may itself save partway through, to
	/// populate a newly-added Module's id before its Tasks reference it).</summary>
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
		var typeCode = module.ProjectType?.Code;
		// Deliberately consistent with the original design: an int? can't
		// distinguish "leave unchanged" from "clear to null", so only a
		// supplied value is ever applied here.
		if (request.ProjectTypeId is int ptId)
		{
			var projectType = await repository.GetProjectTypeByIdAsync(ptId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), ptId);
			module.ProjectTypeId = ptId;
			typeCode = projectType.Code;
		}

		await repository.SaveChangesAsync(ct);
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, module.ProjectTypeId, typeCode);
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
			DefaultBillable = false,
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
			.OrderByDescending(d => d.ResourceCount)
			.ThenBy(d => d.ProjectName)
			.ToList();
	}

	public async Task<IReadOnlyList<AllocatedEmployeeDto>> GetAllocatedEmployeesAsync(int projectId, CancellationToken ct = default)
	{
		var project = await repository.GetProjectWithAllocationsByIdAsync(projectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), projectId);

		return project.EmployeeAllocations
			.Where(a => a.Employee is not null)
			.Select(a => new AllocatedEmployeeDto(a.Employee!.EmployeeId, a.Employee.EmployeeCode, a.Employee.FullName, a.Employee.Department?.Name ?? ""))
			.OrderBy(e => e.FullName)
			.ToList();
	}

	// ---- Shared helpers ----

	private async Task RequireDepartmentExistsAsync(int departmentId, CancellationToken ct)
	{
		var departments = await repository.GetDepartmentsAsync(ct);
		if (!departments.Any(d => d.DepartmentId == departmentId))
			throw new EntityNotFoundException(nameof(Department), departmentId);
	}

	private async Task ValidateLeadershipEmployeesAsync(int? leadId, int? managerId, int? deliveryHeadId, CancellationToken ct)
	{
		foreach (var id in new[] { leadId, managerId, deliveryHeadId })
			if (id is int empId && await employeeRepository.GetByIdAsync(empId, ct) is null)
				throw new EntityNotFoundException(nameof(Employee), empId);
	}

	/// <summary>Finds (or lazily creates) the internal "Pending Classification"
	/// account that self-service "Others" quick-added projects land under until
	/// an admin properly classifies them.</summary>
	private async Task<Account> GetOrCreatePendingClassificationAccountAsync(CancellationToken ct)
	{
		var accounts = await repository.GetAccountsAsync(ct);
		var existing = accounts.FirstOrDefault(a => a.Name == "Pending Classification");
		if (existing is not null) return existing;

		var departments = await repository.GetDepartmentsAsync(ct);
		var defaultDepartment = departments.FirstOrDefault()
			?? throw new BusinessRuleException("Cannot auto-create the \"Pending Classification\" account - no departments exist yet.");

		var account = new Account
		{
			Name = "Pending Classification",
			DepartmentId = defaultDepartment.DepartmentId,
			AccountType = AccountType.Internal,
			CreatedAt = DateTime.UtcNow,
		};
		await repository.AddAccountAsync(account, ct);
		await repository.SaveChangesAsync(ct);
		return account;
	}

	private async Task<string> GenerateNextPendingProjectCodeAsync(CancellationToken ct)
	{
		var existing = await repository.GetProjectsAsync(ct);
		var maxNumber = existing
			.Where(p => p.Code.StartsWith("PEND", StringComparison.OrdinalIgnoreCase))
			.Select(p => int.TryParse(p.Code.AsSpan(4), out var n) ? n : 0)
			.DefaultIfEmpty(0)
			.Max();
		return $"PEND{(maxNumber + 1):D4}";
	}

	private static AccountType ParseAccountType(string value) =>
		Enum.TryParse<AccountType>(value, out var parsed)
			? parsed
			: throw new BusinessRuleException($"Account type must be \"Customer\" or \"Internal\" (got \"{value}\").");

	private static AccountDto ToDto(Account a) => new(a.AccountId, a.DepartmentId, a.Name, a.AccountType.ToString());

	private static ProjectDto ToDto(Project p) => new(
		p.ProjectId, p.AccountId, p.Code, p.Name, p.DefaultBillable, p.IsActive,
		p.ProjectTypeId, p.ProjectType?.Name,
		p.ProjectTech, p.BillingType, p.CustomerPO, p.Notes, p.NeedsReview,
		p.ProjectLeadEmployeeId, p.ProjectLeadEmployee?.FullName,
		p.ProjectManagerEmployeeId, p.ProjectManagerEmployee?.FullName,
		p.DeliveryHeadEmployeeId, p.DeliveryHeadEmployee?.FullName
	);

	private static ModuleDto ToDto(Module m) => new(m.ModuleId, m.ProjectId, m.Name, m.ProjectTypeId, m.ProjectType?.Code);
	private static WorkTaskDto ToDto(WorkTask t) => new(t.TaskId, t.ModuleId, t.Name);
	private static HolidayDto ToDto(Holiday h) => new(h.HolidayId, h.HolidayDate, h.Name, h.Location, h.AccountId);
}
