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

		var name = request.Name?.Trim() ?? "";
		if (name.Length == 0)
			throw new BusinessRuleException("Enter a user story name.");
		if (name.Length > MaxNameLength)
			throw new BusinessRuleException($"The user story name is {name.Length} characters - please keep it to {MaxNameLength} or fewer.");

		var project = await masterDataRepository.GetProjectByIdAsync(request.ProjectId, ct)
			?? throw new EntityNotFoundException(nameof(Project), request.ProjectId);
		if (!project.IsActive)
			throw new BusinessRuleException($"Project \"{project.Name}\" is inactive - pick another project.");

		// Same rule as Add Task Line: you log time only on projects you're allocated to.
		var allocations = await employeeRepository.GetProjectAllocationsAsync(employee.EmployeeId, ct);
		if (allocations.All(a => a.ProjectId != project.ProjectId))
			throw new BusinessRuleException($"You aren't allocated to \"{project.Name}\". Ask your admin to allocate you first.");

		var existing = await userStoryRepository.GetForEmployeeAsync(employee.EmployeeId, ct);
		if (existing.Any(s => s.IsActive && s.ProjectId == project.ProjectId && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
			throw new BusinessRuleException($"You already have a user story called \"{name}\" in this project.");

		var story = new UserStory
		{
			EmployeeId = employee.EmployeeId,
			ProjectId = project.ProjectId,
			Name = name,
			CreatedAt = DateTime.UtcNow,
			Project = project,
		};
		await userStoryRepository.AddAsync(story, ct);
		await userStoryRepository.SaveChangesAsync(ct);
		return ToDto(story);
	}

	private async Task<Employee> RequireEmployeeAsync(string employeeCode, CancellationToken ct) =>
		await employeeRepository.GetByCodeAsync(employeeCode, ct)
			?? throw new EntityNotFoundException(nameof(Employee), employeeCode);

	private static UserStoryDto ToDto(UserStory s) =>
		new(s.UserStoryId, s.Name, s.ProjectId, s.Project?.Name ?? "", s.IsActive, s.CreatedAt);
}
