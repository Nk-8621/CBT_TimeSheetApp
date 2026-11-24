using Meridian.Domain.Enums;

namespace Meridian.Domain.Entities;

/// <summary>
/// A client (Customer) or internal practice (Internal) that projects roll up to.
/// NOTE: seed data here is still placeholder/fictional pending real client data.
/// </summary>
public class Account
{
    public int AccountId { get; set; }
    public int DepartmentId { get; set; }
    public required string Name { get; set; }
    public AccountType AccountType { get; set; }
    /// <summary>CLIENT WEEK OFF (Nov 2026): the client's default week-off days,
    /// e.g. "Sat,Sun". Employees whose primary client is this one follow them,
    /// unless they are in a custom week off (Master Data -> Custom WeekOffs).</summary>
    public string WeekOffDays { get; set; } = "Sat,Sun";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Department? Department { get; set; }
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
