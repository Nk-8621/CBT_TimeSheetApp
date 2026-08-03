namespace Meridian.Domain.Entities;

/// <summary>
/// Meridian *product-access* roles (EMP/LEAD/L2/ADMIN) — which screens/nav
/// a person can see. NOT the same thing as the org chart's job titles, and
/// NOT the same as being someone's manager (see Employee.ManagerEmployeeId).
/// </summary>
public class Role
{
    public int RoleId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }

    public ICollection<EmployeeRole> EmployeeRoles { get; set; } = new List<EmployeeRole>();
}
