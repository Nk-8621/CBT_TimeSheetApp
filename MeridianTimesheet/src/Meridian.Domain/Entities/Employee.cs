namespace Meridian.Domain.Entities;

public class Employee
{
    public int EmployeeId { get; set; }

    /// <summary>Real org ID, e.g. "CBT1000".</summary>
    public required string EmployeeCode { get; set; }
    public required string FullName { get; set; }
    public required string Initials { get; set; }

    public int DepartmentId { get; set; }
    public int LocationId { get; set; }

    /// <summary>Cleaned job title, grade stripped out (e.g. "Principal Engineer").</summary>
    public required string Designation { get; set; }

    /// <summary>E0-E6 / S1-S3. Null where source data had none.</summary>
    public string? Grade { get; set; }

    /// <summary>Original unparsed job title string, kept for audit/traceability.</summary>
    public required string JobTitleRaw { get; set; }

    /// <summary>Direct reporting manager — the real "Reports to" relationship, any depth.</summary>
    public int? ManagerEmployeeId { get; set; }

    public string? Email { get; set; }

    /// <summary>Populated on first Microsoft login; links to Entra ID.</summary>
    public Guid? EntraObjectId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Department? Department { get; set; }
    public Location? Location { get; set; }
    public Employee? Manager { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
    public ICollection<EmployeeRole> EmployeeRoles { get; set; } = new List<EmployeeRole>();
    public ICollection<EmployeeDepartment> EmployeeDepartments { get; set; } = new List<EmployeeDepartment>();
}
