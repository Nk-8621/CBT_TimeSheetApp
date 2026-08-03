namespace Meridian.Domain.Entities;

public class Module
{
    public int ModuleId { get; set; }
    public int ProjectId { get; set; }
    public int TaskCategoryId { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }

    public Project? Project { get; set; }
    public TaskCategory? TaskCategory { get; set; }
    public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
}
