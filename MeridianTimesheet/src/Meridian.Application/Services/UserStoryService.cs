using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

public class UserStoryService(
	IEmployeeRepository employeeRepository,
	IUserStoryRepository userStoryRepository,
	IMasterDataRepository masterDataRepository) : IUserStoryService
{
	private const int MaxNameLength = 200;

	/// <summary>Code of a department's automatic "Internal Work" project,
	/// followed by the department id (e.g. INT-GEN-12). Project codes are
	/// limited to 20 characters, so the id is used instead of the name.</summary>
	private const string InternalProjectCodePrefix = "INT-GEN-";

	public async Task<IReadOnlyList<UserStoryDto>> GetForEmployeeAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var stories = await userStoryRepository.GetForEmployeeAsync(employee.EmployeeId, ct);
		var taskCounts = await userStoryRepository.GetTaskCountsAsync(employee.EmployeeId, ct);
		return stories.Select(s => ToDto(s, taskCounts.GetValueOrDefault(s.UserStoryId))).ToList();
	}

	public async Task<UserStoryDto> CreateAsync(string employeeCode, CreateUserStoryRequest request, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var name = ValidateName(request.Name);
		ValidateDates(request.StartDate, request.EndDate);

		var project = request.ProjectId is int projectId
			? await RequireAllocatedProjectAsync(employee, projectId, ct)
			: await GetInternalWorkProjectAsync(employee, ct);

		await EnsureNameNotTakenAsync(employee.EmployeeId, project.ProjectId, name, excludeStoryId: null, ct);

		var story = new UserStory
		{
			EmployeeId = employee.EmployeeId,
			ProjectId = project.ProjectId,
			Name = name,
			StartDate = request.StartDate,
			EndDate = request.EndDate,
			CreatedAt = DateTime.UtcNow,
			Project = project,
		};
		await userStoryRepository.AddAsync(story, ct);
		await userStoryRepository.SaveChangesAsync(ct);
		return ToDto(story, taskCount: 0);
	}

	public async Task<UserStoryDto> UpdateAsync(string employeeCode, int userStoryId, UpdateUserStoryRequest request, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var story = await userStoryRepository.GetByIdAsync(userStoryId, ct)
			?? throw new EntityNotFoundException(nameof(UserStory), userStoryId);
		if (story.EmployeeId != employee.EmployeeId)
			throw new BusinessRuleException("This user story belongs to another employee.");

		var name = ValidateName(request.Name);
		ValidateDates(request.StartDate, request.EndDate);
		await EnsureNameNotTakenAsync(employee.EmployeeId, story.ProjectId, name, excludeStoryId: story.UserStoryId, ct);

		// Tasks already logged outside the new dates are deliberately left alone:
		// they stay in their weeks as history. The new dates only limit where
		// NEW hours can be added (see TimesheetService).
		story.Name = name;
		story.StartDate = request.StartDate;
		story.EndDate = request.EndDate;
		story.IsActive = true;
		story.UpdatedAt = DateTime.UtcNow;
		await userStoryRepository.SaveChangesAsync(ct);

		var taskCounts = await userStoryRepository.GetTaskCountsAsync(employee.EmployeeId, ct);
		return ToDto(story, taskCounts.GetValueOrDefault(story.UserStoryId));
	}

	public async Task DeleteAsync(string employeeCode, int userStoryId, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var story = await userStoryRepository.GetByIdAsync(userStoryId, ct)
			?? throw new EntityNotFoundException(nameof(UserStory), userStoryId);
		if (story.EmployeeId != employee.EmployeeId)
			throw new BusinessRuleException("This user story belongs to another employee.");

		// Only an unused story can go - hours already logged under it stay safe.
		var taskCounts = await userStoryRepository.GetTaskCountsAsync(employee.EmployeeId, ct);
		if (taskCounts.GetValueOrDefault(story.UserStoryId) > 0)
			throw new BusinessRuleException($"\"{story.Name}\" has tasks logged under it - remove its tasks first, then delete the story.");

		userStoryRepository.Remove(story);
		await userStoryRepository.SaveChangesAsync(ct);
	}

	/// <summary>The project the employee picked: it must be active and one he
	/// is allocated to (same rule as Add Task Line).</summary>
	private async Task<Project> RequireAllocatedProjectAsync(Employee employee, int projectId, CancellationToken ct)
	{
		var project = await masterDataRepository.GetProjectByIdAsync(projectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), projectId);
		if (!project.IsActive)
			throw new BusinessRuleException($"Project \"{project.Name}\" is inactive - pick another project.");

		var allocations = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		if (allocations.All(a => a.ProjectId != project.ProjectId))
			throw new BusinessRuleException($"You aren't allocated to \"{project.Name}\". Ask your admin to allocate you first.");
		return project;
	}

	/// <summary>No project picked. Allowed only for an employee with no active
	/// allocated projects (e.g. HR, Sales): his story goes to his department's
	/// "Internal Work" project - non-billable, under a "(Internal)" account of
	/// that department, so reports still show the time under his department.
	/// The account and project are created the first time they're needed.</summary>
	private async Task<Project> GetInternalWorkProjectAsync(Employee employee, CancellationToken ct)
	{
		var allocations = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		var activeProjectIds = (await masterDataRepository.GetProjectsAsync(ct))
			.Where(p => p.IsActive).Select(p => p.ProjectId).ToHashSet();
		if (allocations.Any(a => activeProjectIds.Contains(a.ProjectId)))
			throw new BusinessRuleException("Select the project this story belongs to.");

		var department = await masterDataRepository.GetDepartmentByIdAsync(employee.DepartmentId, ct)
			?? throw new EntityNotFoundException(nameof(Department), employee.DepartmentId);

		// Reuse an existing project with this code (it is only ever made here).
		var code = $"{InternalProjectCodePrefix}{department.DepartmentId}";
		var existing = (await masterDataRepository.GetProjectsAsync(ct)).FirstOrDefault(p => p.Code == code);
		if (existing is not null)
			return await masterDataRepository.GetProjectByIdAsync(existing.ProjectId, ct) ?? existing;

		var accountName = $"{department.Name} (Internal)";
		var account = await masterDataRepository.GetAccountByNameAsync(accountName, ct);
		if (account is null)
		{
			account = new Account
			{
				DepartmentId = department.DepartmentId,
				Name = accountName,
				AccountType = AccountType.Internal,
				CreatedAt = DateTime.UtcNow,
			};
			await masterDataRepository.AddAccountAsync(account, ct);
			await masterDataRepository.SaveChangesAsync(ct);
		}

		var project = new Project
		{
			AccountId = account.AccountId,
			Code = code,
			Name = $"{department.Name} – Internal Work",
			DefaultBillable = "NonBillable",
			BillingType = "Others",
			IsActive = true,
			NeedsReview = false,
			CreatedAt = DateTime.UtcNow,
		};
		await masterDataRepository.AddProjectAsync(project, ct);
		await masterDataRepository.SaveChangesAsync(ct);
		return project;
	}

	private static string ValidateName(string? rawName)
	{
		var name = rawName?.Trim() ?? "";
		if (name.Length == 0)
			throw new BusinessRuleException("Enter a user story name.");
		if (name.Length > MaxNameLength)
			throw new BusinessRuleException($"The user story name is {name.Length} characters - please keep it to {MaxNameLength} or fewer.");
		return name;
	}

	private static void ValidateDates(DateOnly startDate, DateOnly endDate)
	{
		if (startDate == default || endDate == default)
			throw new BusinessRuleException("Enter both a start date and an end date.");
		if (endDate < startDate)
			throw new BusinessRuleException("The end date can't be before the start date.");
	}

	/// <summary>One story name per project per employee - to run the same work
	/// again later, the employee edits the old story's dates instead of making
	/// a second copy (the Story history link in the drawer does exactly that).</summary>
	private async Task EnsureNameNotTakenAsync(int employeeId, int projectId, string name, int? excludeStoryId, CancellationToken ct)
	{
		var existing = await userStoryRepository.GetForEmployeeAsync(employeeId, ct);
		if (existing.Any(s => s.UserStoryId != excludeStoryId
			&& s.ProjectId == projectId
			&& string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
			throw new BusinessRuleException($"You already have a user story called \"{name}\" in this project - pick it from Story history and change its dates instead.");
	}

	private async Task<Employee> RequireEmployeeAsync(string employeeCode, CancellationToken ct) =>
		await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

	private static UserStoryDto ToDto(UserStory s, int taskCount) =>
		new(s.UserStoryId, s.Name, s.ProjectId, s.Project?.Name ?? "", s.StartDate, s.EndDate, s.IsActive, s.CreatedAt, taskCount);
}
