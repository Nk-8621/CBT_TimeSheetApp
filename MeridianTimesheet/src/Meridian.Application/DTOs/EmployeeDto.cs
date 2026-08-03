namespace Meridian.Application.DTOs;

public record EmployeeDto(
    int Id,
    string EmployeeCode,
    string FullName,
    string Initials,
    int DepartmentId,
    int LocationId,
    string Designation,
    string? Grade,
    int? ManagerEmployeeId,
    string? ManagerName
);

public record RoleDto(string Code, string Name, IReadOnlyList<string> AllowedNavKeys);
