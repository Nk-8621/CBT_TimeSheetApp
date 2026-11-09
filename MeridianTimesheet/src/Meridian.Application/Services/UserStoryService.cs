using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;

namespace Meridian.Application.Services;

public class UserStoryService(
	IEmployeeRepository employeeRepository,
	IUserStoryRepository userStoryRepository,
	IMasterDataRepository masterDataRepository) : IUserStoryService
{
	private const int MaxNameLength = 200;

	public async Task<IReadOnlyList<UserStoryDto>> GetForEmployeeAsync(string employeeCode, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var stories = await userStoryRepository.GetForEmployeeAsync(employee.EmployeeId, ct);
		return stories.Select(ToDto).ToList();
	}

	public async Task<UserStoryDto> CreateAsync(string employeeCode, CreateUserStoryRequest request, CancellationToken ct = default)
	{
		var employee = await RequireEmployeeAsync(employeeCode, ct);
		var name = ValidateName(request.Name);
		ValidateDates(request.StartDate, request.EndDate);

		var project = await masterDataRepository.GetProjectByIdAsync(request.ProjectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), request.ProjectId);
		if (!project.IsActive)
			throw new BusinessRuleException($"Project \"{project.Name}\" is inactive - pick another project.");

		// Same rule as Add Task Line: you log time only on projects you're allocated to.
		var allocations = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		if (allocations.All(a => a.ProjectId != project.ProjectId))
			throw new BusinessRuleException($"You aren't allocated to \"{project.Name}\". Ask your admin to allocate you first.");

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
		return ToDto(story);
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
		return ToDto(story);
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

	private static UserStoryDto ToDto(UserStory s) =>
		new(s.UserStoryId, s.Name, s.ProjectId, s.Project?.Name ?? "", s.StartDate, s.EndDate, s.IsActive, s.CreatedAt);
}
