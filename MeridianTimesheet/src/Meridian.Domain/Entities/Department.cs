namespace Meridian.Domain.Entities;

/// <summary>
/// Organizational department. Supports one level of parent/child nesting
/// (e.g. "SAP" is the parent of "SAP > Technical" and "SAP > Functional"),
/// matching the real org structure rather than a flat list.
/// </summary>
public class Department
{
    public int DepartmentId { get; set; }
    public int? ParentDepartmentId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Department? Parent { get; set; }
    public ICollection<Department> Children { get; set; } = new List<Department>();
}
