namespace Meridian.Domain.Entities;

public class Project
{
    public int ProjectId { get; set; }
    public int AccountId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool DefaultBillable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Account? Account { get; set; }
    public ICollection<Module> Modules { get; set; } = new List<Module>();
}
