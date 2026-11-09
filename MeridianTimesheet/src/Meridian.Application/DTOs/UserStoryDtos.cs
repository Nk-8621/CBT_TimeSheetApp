namespace Meridian.Application.DTOs;

public record UserStoryDto(
	int Id,
	string Name,
	int ProjectId,
	string ProjectName,
	bool IsActive,
	DateTime CreatedAt
);

public record CreateUserStoryRequest(string Name, int ProjectId);

/// <summary>Adds one task (a timesheet line) under a user story for one week.
/// The project comes from the story; the employee only gives the task name
/// and the hours per day.</summary>
public record CreateStoryTaskRequest(int UserStoryId, string TaskName, decimal[] HoursByDay);
