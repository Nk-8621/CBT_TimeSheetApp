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

    /// <summary>Drives which Modules/Tasks auto-generate for this project. Null
    /// only for projects created before this feature shipped.</summary>
    public int? ProjectTypeId { get; set; }

    /// <summary>Free-text technology tag (e.g. "Digital IOT", "Analytics",
    /// "SAP Staffing", "MES") - no fixed list supplied, kept as free text.</summary>
    public string? ProjectTech { get; set; }

    /// <summary>Fixed, T&M, Consumption, NB-ValueAdd, NB-L&D, NB-Training,
    /// NB-Travel, or Others - enforced in the application layer, not a DB
    /// CHECK constraint (same convention as AccountType/RequestType).</summary>
    public string? BillingType { get; set; }

    public string? CustomerPO { get; set; }
    public string? Notes { get; set; }

    public int? ProjectLeadEmployeeId { get; set; }
    public int? ProjectManagerEmployeeId { get; set; }
    public int? DeliveryHeadEmployeeId { get; set; }

    /// <summary>True for a project auto-created on the fly via the "Others"
    /// quick-add flow (Add Task Line) - missing a real Account/Code/BillingType
    /// until admin reviews and fills them in on the Master Data screen.</summary>
    public bool NeedsReview { get; set; }

    public int? ProjectTypeId { get; set; }
    public string? ProjectTech { get; set; }
    public string? BillingType { get; set; }
    public string? CustomerPO { get; set; }
    public string? Notes { get; set; }

    /// <summary>True for a project created via the "Others" self-service
    /// quick-add path (pending an admin classifying it properly) rather
    /// than through the full admin Add Project form.</summary>
    public bool NeedsReview { get; set; }

    public int? ProjectLeadEmployeeId { get; set; }
    public int? ProjectManagerEmployeeId { get; set; }
    public int? DeliveryHeadEmployeeId { get; set; }

    public Account? Account { get; set; }
    public ProjectType? ProjectType { get; set; }
    public Employee? ProjectLeadEmployee { get; set; }
    public Employee? ProjectManagerEmployee { get; set; }
    public Employee? DeliveryHeadEmployee { get; set; }
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<EmployeeProjectAllocation> EmployeeAllocations { get; set; } = new List<EmployeeProjectAllocation>();
}
