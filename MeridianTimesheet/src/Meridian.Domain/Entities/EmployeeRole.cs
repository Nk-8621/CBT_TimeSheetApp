namespace Meridian.Domain.Entities;

/// <summary>Many-to-many join: an employee can hold more than one product-access role.</summary>
public class EmployeeRole
{
    public int EmployeeId { get; set; }
    public int RoleId { get; set; }
    public DateTime AssignedAt { get; set; }

    public Employee? Employee { get; set; }
    public Role? Role { get; set; }
}
