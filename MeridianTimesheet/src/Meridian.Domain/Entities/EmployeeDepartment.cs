namespace Meridian.Domain.Entities;

/// <summary>
/// Handles the rare case of an employee spanning more than one department
/// (seen in real data: one person listed under both Splunk and MCB).
/// IsPrimary marks which one matches Employee.DepartmentId.
/// </summary>
public class EmployeeDepartment
{
    public int EmployeeId { get; set; }
    public int DepartmentId { get; set; }
    public bool IsPrimary { get; set; }

    public Employee? Employee { get; set; }
    public Department? Department { get; set; }
}
