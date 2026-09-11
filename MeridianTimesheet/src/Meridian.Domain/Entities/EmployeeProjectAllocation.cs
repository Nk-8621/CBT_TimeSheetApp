namespace Meridian.Domain.Entities;

/// <summary>Many-to-many join: which Projects an Employee is actually
/// working on right now, independent of (and possibly spanning) their own
/// home Department - an employee can be allocated to projects under any
/// Department. One row per Employee/Project pair; admin ticks/unticks these
/// from the Add/Edit Employee form.</summary>
public class EmployeeProjectAllocation
{
    public int EmployeeId { get; set; }
    public int ProjectId { get; set; }
    public DateTime AssignedAt { get; set; }

    public Employee? Employee { get; set; }
    public Project? Project { get; set; }
}
