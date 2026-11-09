namespace Meridian.Application.DTOs;

public record UserStoryDto(
	int Id,
	string Name,
	int ProjectId,
	string ProjectName,
	DateOnly StartDate,
	DateOnly EndDate,
	bool IsActive,
	DateTime CreatedAt
);

public record CreateUserStoryRequest(string Name, int ProjectId, DateOnly StartDate, DateOnly EndDate);

/// <summary>Edit a story's name and dates. The project can't change, because
/// the story's tasks are already logged (and billed) against it.</summary>
public record UpdateUserStoryRequest(string Name, DateOnly StartDate, DateOnly EndDate);

/// <summary>Adds one task (a timesheet line) under a user story for one week.
/// The project comes from the story; the employee only gives the task name
/// and the hours per day.</summary>
public record CreateStoryTaskRequest(int UserStoryId, string TaskName, decimal[] HoursByDay);
