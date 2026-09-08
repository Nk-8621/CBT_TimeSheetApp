using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class MasterDataService(IMasterDataRepository repository, IEmployeeRepository employeeRepository) : IMasterDataService
{
	/// <summary>Fixed, T&M, Consumption, NB-ValueAdd, NB-L&D, NB-Training, NB-Travel, Others.</summary>
	private static readonly string[] AllowedBillingTypes =
		["Fixed", "T&M", "Consumption", "NB-ValueAdd", "NB-L&D", "NB-Training", "NB-Travel", "Others"];

	/// <summary>Account new "Others"-created projects roll up to until admin
	/// assigns the real one. Seeded by the schema script - see
	/// 07_add_project_type_and_project_fields.sql.</summary>
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

		var existing = await repository.GetProjectsAsync(ct);
		if (existing.Any(p => string.Equals(p.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
			throw new BusinessRuleException($"A project with code \"{request.Code}\" already exists.");

		_ = await repository.GetAccountByIdAsync(request.AccountId, ct)
			?? throw new EntityNotFoundException(nameof(Account), request.AccountId);

		if (request.BillingType is not null && !AllowedBillingTypes.Contains(request.BillingType))
			throw new BusinessRuleException($"Billing type must be one of: {string.Join(", ", AllowedBillingTypes)}.");

		await RequireEmployeeExistsIfSetAsync(request.ProjectLeadEmployeeId, ct);
		await RequireEmployeeExistsIfSetAsync(request.ProjectManagerEmployeeId, ct);
		await RequireEmployeeExistsIfSetAsync(request.DeliveryHeadEmployeeId, ct);

		ProjectType? projectType = null;
		if (request.ProjectTypeId is int ptId)
			projectType = await repository.GetProjectTypeByIdAsync(ptId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), ptId);

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

		if (request.ProjectTypeId is int)
			await GenerateModulesFromProjectTypeAsync(project.ProjectId, request.ProjectTypeId!.Value, ct);

		return await ReloadProjectDtoAsync(project.ProjectId, ct);
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

		if (request.BillingType is not null)
		{
			if (!AllowedBillingTypes.Contains(request.BillingType))
				throw new BusinessRuleException($"Billing type must be one of: {string.Join(", ", AllowedBillingTypes)}.");
			project.BillingType = request.BillingType;
		}
		if (request.ProjectTech is not null) project.ProjectTech = request.ProjectTech;
		if (request.CustomerPO is not null) project.CustomerPO = request.CustomerPO;
		if (request.Notes is not null) project.Notes = request.Notes;

		if (request.ProjectLeadEmployeeId is int leadId) { await RequireEmployeeExistsAsync(leadId, ct); project.ProjectLeadEmployeeId = leadId; }
		if (request.ProjectManagerEmployeeId is int mgrId) { await RequireEmployeeExistsAsync(mgrId, ct); project.ProjectManagerEmployeeId = mgrId; }
		if (request.DeliveryHeadEmployeeId is int dhId) { await RequireEmployeeExistsAsync(dhId, ct); project.DeliveryHeadEmployeeId = dhId; }

		project.UpdatedAt = DateTime.UtcNow;

		await repository.SaveChangesAsync(ct);
		return await ReloadProjectDtoAsync(projectId, ct);
	}

	// ---- Module ----

	public async Task<ModuleDto> CreateModuleAsync(CreateModuleRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Module name is required.");

		_ = await repository.GetProjectByIdAsync(request.ProjectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), request.ProjectId);

		ProjectType? projectType = null;
		if (request.ProjectTypeId is int ptId)
			projectType = await repository.GetProjectTypeByIdAsync(ptId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), ptId);

		var module = new Module { Name = request.Name, ProjectId = request.ProjectId, ProjectTypeId = request.ProjectTypeId, CreatedAt = DateTime.UtcNow };
		await repository.AddModuleAsync(module, ct);
		await repository.SaveChangesAsync(ct);
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, projectType?.ProjectTypeId, projectType?.Code);
	}

	public async Task<ModuleDto> UpdateModuleAsync(int moduleId, UpdateModuleRequest request, CancellationToken ct = default)
	{
		var module = await repository.GetModuleByIdAsync(moduleId, ct)
			?? throw new EntityNotFoundException(nameof(Module), moduleId);

		if (request.Name is not null) module.Name = request.Name;
		if (request.ProjectTypeId is int ptId)
		{
			_ = await repository.GetProjectTypeByIdAsync(ptId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), ptId);
			module.ProjectTypeId = ptId;
		}

		await repository.SaveChangesAsync(ct);

		// Reload the ProjectType's Code for the DTO regardless of whether it was
		// just changed - otherwise an unrelated rename (Name-only update) would
		// come back with ProjectTypeCode=null even though ProjectTypeId is set.
		var currentType = module.ProjectTypeId is int currentPtId
			? await repository.GetProjectTypeByIdAsync(currentPtId, ct)
			: null;
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, module.ProjectTypeId, currentType?.Code);
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

	// ---- Project Type template management (Admin only) ----

	public async Task<ProjectTypeDto> CreateProjectTypeAsync(CreateProjectTypeRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Project type code and name are both required.");

		var existing = await repository.GetProjectTypesAsync(ct);
		if (existing.Any(t => string.Equals(t.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
			throw new BusinessRuleException($"A project type with code \"{request.Code}\" already exists.");

		var type = new ProjectType { Code = request.Code, Name = request.Name };
		await repository.AddProjectTypeAsync(type, ct);
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeDto(type.ProjectTypeId, type.Code, type.Name);
	}

	public async Task<ProjectTypeDto> UpdateProjectTypeAsync(int projectTypeId, UpdateProjectTypeRequest request, CancellationToken ct = default)
	{
		var type = await repository.GetProjectTypeByIdAsync(projectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), projectTypeId);

		if (request.Code is not null) type.Code = request.Code;
		if (request.Name is not null) type.Name = request.Name;
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeDto(type.ProjectTypeId, type.Code, type.Name);
	}

	public async Task DeleteProjectTypeAsync(int projectTypeId, DeleteProjectTypeRequest request, CancellationToken ct = default)
	{
		var type = await repository.GetProjectTypeByIdAsync(projectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), projectTypeId);

		var affectedProjects = await repository.GetProjectsByProjectTypeIdAsync(projectTypeId, ct);
		if (affectedProjects.Count > 0)
		{
			if (request.ReplacementProjectTypeId is not int replacementId)
				throw new BusinessRuleException(
					$"{affectedProjects.Count} project(s) still use this Project Type - pick a replacement Project Type to reassign them to before deleting.");

			if (replacementId == projectTypeId)
				throw new BusinessRuleException("Replacement Project Type must be different from the one being deleted.");

			_ = await repository.GetProjectTypeByIdAsync(replacementId, ct)
				?? throw new EntityNotFoundException(nameof(ProjectType), replacementId);

			foreach (var project in affectedProjects)
				project.ProjectTypeId = replacementId;
		}

		repository.RemoveProjectType(type);
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

	public async Task<ProjectTypeModuleTemplateDto> UpdateModuleTemplateAsync(int id, UpdateProjectTypeModuleTemplateRequest request, CancellationToken ct = default)
	{
		var template = await repository.GetModuleTemplateByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeModuleTemplate), id);
		if (request.Name is not null) template.Name = request.Name;
		if (request.SortOrder is int sort) template.SortOrder = sort;
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeModuleTemplateDto(template.ProjectTypeModuleTemplateId, template.Name, template.SortOrder, []);
	}

	public async Task DeleteModuleTemplateAsync(int id, CancellationToken ct = default)
	{
		var template = await repository.GetModuleTemplateByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeModuleTemplate), id);
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

	public async Task<ProjectTypeTaskTemplateDto> UpdateTaskTemplateAsync(int id, UpdateProjectTypeTaskTemplateRequest request, CancellationToken ct = default)
	{
		var template = await repository.GetTaskTemplateByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeTaskTemplate), id);
		if (request.Name is not null) template.Name = request.Name;
		if (request.SortOrder is int sort) template.SortOrder = sort;
		await repository.SaveChangesAsync(ct);
		return new ProjectTypeTaskTemplateDto(template.ProjectTypeTaskTemplateId, template.Name, template.SortOrder);
	}

	public async Task DeleteTaskTemplateAsync(int id, CancellationToken ct = default)
	{
		var template = await repository.GetTaskTemplateByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(ProjectTypeTaskTemplate), id);
		repository.RemoveTaskTemplate(template);
		await repository.SaveChangesAsync(ct);
	}

	// ---- "Others" quick-add (employee-reachable) ----

	public async Task<ProjectDto> QuickAddProjectAsync(QuickAddProjectRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Project name is required.");

		var pendingAccount = await repository.GetAccountByNameAsync(PendingClassificationAccountName, ct)
			?? throw new BusinessRuleException(
				$"The \"{PendingClassificationAccountName}\" account is missing - run 07_add_project_type_and_project_fields.sql first.");

		var placeholderCode = "OTH-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

		var project = new Project
		{
			Name = request.Name.Trim(),
			Code = placeholderCode,
			AccountId = pendingAccount.AccountId,
			DefaultBillable = true,
			IsActive = true,
			NeedsReview = true,
			CreatedAt = DateTime.UtcNow,
		};
		await repository.AddProjectAsync(project, ct);
		await repository.SaveChangesAsync(ct);
		return await ReloadProjectDtoAsync(project.ProjectId, ct);
	}

	public async Task<ModuleDto> QuickAddModuleAsync(QuickAddModuleRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Module name is required.");
		_ = await repository.GetProjectByIdAsync(request.ProjectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), request.ProjectId);

		// ProjectTypeId left null on purpose - an ad hoc "Others" module isn't
		// part of any Project Type's fixed template.
		var module = new Module { Name = request.Name.Trim(), ProjectId = request.ProjectId, ProjectTypeId = null, CreatedAt = DateTime.UtcNow };
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

		var task = new WorkTask { Name = request.Name.Trim(), ModuleId = request.ModuleId, CreatedAt = DateTime.UtcNow };
		await repository.AddTaskAsync(task, ct);
		await repository.SaveChangesAsync(ct);
		return ToDto(task);
	}

	// ---- Shared helpers ----

	/// <summary>Creates one real Module per the Project Type's Level-1 template
	/// row, each with one real WorkTask per its Level-2 rows. If the Project
	/// Type has no template rows at all (the old placeholder categories -
	/// consult/dev/bi/support/presales/train/admin), falls back to the legacy
	/// single flat "General" module from TaskTemplates.ByCategory, keyed by
	/// the Project Type's Code, so existing behavior for those isn't lost.</summary>
	private async Task GenerateModulesFromProjectTypeAsync(int projectId, int projectTypeId, CancellationToken ct)
	{
		var type = await repository.GetProjectTypeWithTemplatesByIdAsync(projectTypeId, ct)
			?? throw new EntityNotFoundException(nameof(ProjectType), projectTypeId);

		if (type.ModuleTemplates.Count > 0)
		{
			foreach (var moduleTemplate in type.ModuleTemplates.OrderBy(m => m.SortOrder))
			{
				var module = new Module
				{
					ProjectId = projectId,
					ProjectTypeId = type.ProjectTypeId,
					Name = moduleTemplate.Name,
					CreatedAt = DateTime.UtcNow,
				};
				await repository.AddModuleAsync(module, ct);
				await repository.SaveChangesAsync(ct); // populates module.ModuleId before its tasks can reference it

				foreach (var taskTemplate in moduleTemplate.TaskTemplates.OrderBy(t => t.SortOrder))
					await repository.AddTaskAsync(new WorkTask { ModuleId = module.ModuleId, Name = taskTemplate.Name, CreatedAt = DateTime.UtcNow }, ct);
				await repository.SaveChangesAsync(ct);
			}
			return;
		}

		if (TaskTemplates.ByCategory.TryGetValue(type.Code, out var taskNames))
		{
			var generalModule = new Module { ProjectId = projectId, ProjectTypeId = type.ProjectTypeId, Name = "General", CreatedAt = DateTime.UtcNow };
			await repository.AddModuleAsync(generalModule, ct);
			await repository.SaveChangesAsync(ct);

			foreach (var taskName in taskNames)
				await repository.AddTaskAsync(new WorkTask { ModuleId = generalModule.ModuleId, Name = taskName, CreatedAt = DateTime.UtcNow }, ct);
			await repository.SaveChangesAsync(ct);
		}
	}

	private async Task<ProjectDto> ReloadProjectDtoAsync(int projectId, CancellationToken ct)
	{
		var project = await repository.GetProjectByIdAsync(projectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), projectId);
		return await ToDtoWithLookupsAsync(project, ct);
	}

	private async Task<ProjectDto> ToDtoWithLookupsAsync(Project p, CancellationToken ct)
	{
		string? projectTypeName = null;
		if (p.ProjectTypeId is int ptId)
		{
			var type = await repository.GetProjectTypeByIdAsync(ptId, ct);
			projectTypeName = type?.Name;
		}
		var lead = p.ProjectLeadEmployeeId is int leadId ? await employeeRepository.GetByIdAsync(leadId, ct) : null;
		var mgr = p.ProjectManagerEmployeeId is int mgrId ? await employeeRepository.GetByIdAsync(mgrId, ct) : null;
		var dh = p.DeliveryHeadEmployeeId is int dhId ? await employeeRepository.GetByIdAsync(dhId, ct) : null;

		return new ProjectDto(
			p.ProjectId, p.AccountId, p.Code, p.Name, p.DefaultBillable, p.IsActive,
			p.ProjectTypeId, projectTypeName, p.ProjectTech, p.BillingType,
			p.CustomerPO, p.Notes, p.NeedsReview,
			p.ProjectLeadEmployeeId, lead?.FullName,
			p.ProjectManagerEmployeeId, mgr?.FullName,
			p.DeliveryHeadEmployeeId, dh?.FullName);
	}

	private async Task RequireDepartmentExistsAsync(int departmentId, CancellationToken ct)
	{
		var departments = await repository.GetDepartmentsAsync(ct);
		if (!departments.Any(d => d.DepartmentId == departmentId))
			throw new EntityNotFoundException(nameof(Department), departmentId);
	}

	private async Task RequireEmployeeExistsIfSetAsync(int? employeeId, CancellationToken ct)
	{
		if (employeeId is int id) await RequireEmployeeExistsAsync(id, ct);
	}

	private async Task RequireEmployeeExistsAsync(int employeeId, CancellationToken ct)
	{
		_ = await employeeRepository.GetByIdAsync(employeeId, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeId);
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
