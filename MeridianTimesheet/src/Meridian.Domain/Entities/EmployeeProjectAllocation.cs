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

    /// <summary>Billable, NonBillable, or PartialBillable - same vocabulary as
    /// TimeEntry.Classification (see BillingClassificationRules). Set by the
    /// admin when allocating this project to this employee, not chosen by the
    /// employee per timesheet line.</summary>
    public string Classification { get; set; } = "Billable";

    /// <summary>AMS/T&amp;M/FB (when Classification is Billable) or OH (when
    /// NonBillable) - null when PartialBillable, which takes no category.
    /// Also null for an allocation created before this feature shipped, until
    /// an admin revisits it.</summary>
    public string? BillingCategory { get; set; }

    public Employee? Employee { get; set; }
    public Project? Project { get; set; }
}
