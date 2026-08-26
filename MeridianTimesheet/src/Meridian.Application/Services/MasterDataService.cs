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
		return modules.Select(m => new ModuleDto(m.ModuleId, m.ProjectId, m.Name, m.TaskCategory?.Code ?? "")).ToList();
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

	public async Task<IReadOnlyList<TaskCategoryDto>> GetTaskCategoriesAsync(CancellationToken ct = default)
	{
		var categories = await repository.GetTaskCategoriesAsync(ct);
		return categories.Select(c => new TaskCategoryDto(c.TaskCategoryId, c.Code, c.Name)).ToList();
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

		var project = new Project
		{
			Name = request.Name,
			Code = request.Code.ToUpperInvariant(),
			AccountId = request.AccountId,
			DefaultBillable = request.DefaultBillable,
			IsActive = true,
			CreatedAt = DateTime.UtcNow,
		};
		await repository.AddProjectAsync(project, ct);
		await repository.SaveChangesAsync(ct); // populates project.ProjectId before a module can reference it

		// Matching the original wireframe: creating a project can immediately
		// seed a starter "General" module with that category's standard task
		// list, so the project is usable on the grid with no second setup step.
		if (!string.IsNullOrWhiteSpace(request.InitialModuleTaskCategoryCode))
		{
			var category = await repository.GetTaskCategoryByCodeAsync(request.InitialModuleTaskCategoryCode, ct)
				?? throw new BusinessRuleException($"Unknown task category \"{request.InitialModuleTaskCategoryCode}\".");

			var generalModule = new Module { ProjectId = project.ProjectId, TaskCategoryId = category.TaskCategoryId, Name = "General", CreatedAt = DateTime.UtcNow };
			await repository.AddModuleAsync(generalModule, ct);
			await repository.SaveChangesAsync(ct);

			if (TaskTemplates.ByCategory.TryGetValue(category.Code, out var taskNames))
			{
				foreach (var taskName in taskNames)
					await repository.AddTaskAsync(new WorkTask { ModuleId = generalModule.ModuleId, Name = taskName, CreatedAt = DateTime.UtcNow }, ct);
				await repository.SaveChangesAsync(ct);
			}
		}

		return ToDto(project);
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
		project.UpdatedAt = DateTime.UtcNow;

		await repository.SaveChangesAsync(ct);
		return ToDto(project);
	}

	// ---- Module ----

	public async Task<ModuleDto> CreateModuleAsync(CreateModuleRequest request, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new BusinessRuleException("Module name is required.");

		_ = await repository.GetProjectByIdAsync(request.ProjectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), request.ProjectId);
		var category = await repository.GetTaskCategoryByCodeAsync(request.TaskCategoryCode, ct)
			?? throw new BusinessRuleException($"Unknown task category \"{request.TaskCategoryCode}\".");

		var module = new Module { Name = request.Name, ProjectId = request.ProjectId, TaskCategoryId = category.TaskCategoryId, CreatedAt = DateTime.UtcNow };
		await repository.AddModuleAsync(module, ct);
		await repository.SaveChangesAsync(ct);
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, category.Code);
	}

	public async Task<ModuleDto> UpdateModuleAsync(int moduleId, UpdateModuleRequest request, CancellationToken ct = default)
	{
		var module = await repository.GetModuleByIdAsync(moduleId, ct)
			?? throw new EntityNotFoundException(nameof(Module), moduleId);

		if (request.Name is not null) module.Name = request.Name;
		string? categoryCode = null;
		if (request.TaskCategoryCode is not null)
		{
			var category = await repository.GetTaskCategoryByCodeAsync(request.TaskCategoryCode, ct)
				?? throw new BusinessRuleException($"Unknown task category \"{request.TaskCategoryCode}\".");
			module.TaskCategoryId = category.TaskCategoryId;
			categoryCode = category.Code;
		}

		await repository.SaveChangesAsync(ct);
		return new ModuleDto(module.ModuleId, module.ProjectId, module.Name, categoryCode ?? request.TaskCategoryCode ?? "");
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

	// ---- Shared helpers ----

	private async Task RequireDepartmentExistsAsync(int departmentId, CancellationToken ct)
	{
		var departments = await repository.GetDepartmentsAsync(ct);
		if (!departments.Any(d => d.DepartmentId == departmentId))
			throw new EntityNotFoundException(nameof(Department), departmentId);
	}

	private static AccountType ParseAccountType(string value) =>
		Enum.TryParse<AccountType>(value, out var parsed)
			? parsed
			: throw new BusinessRuleException($"Account type must be \"Customer\" or \"Internal\" (got \"{value}\").");

	private static AccountDto ToDto(Account a) => new(a.AccountId, a.DepartmentId, a.Name, a.AccountType.ToString());
	private static ProjectDto ToDto(Project p) => new(p.ProjectId, p.AccountId, p.Code, p.Name, p.DefaultBillable, p.IsActive);
	private static WorkTaskDto ToDto(WorkTask t) => new(t.TaskId, t.ModuleId, t.Name);
	private static HolidayDto ToDto(Holiday h) => new(h.HolidayId, h.HolidayDate, h.Name, h.Location, h.AccountId);
}
