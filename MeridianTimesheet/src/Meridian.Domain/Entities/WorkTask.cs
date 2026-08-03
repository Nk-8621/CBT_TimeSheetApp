namespace Meridian.Domain.Entities;

/// <summary>
/// Maps to Carbynetech_Task. Named "WorkTask" rather than "Task" to avoid
/// colliding with System.Threading.Tasks.Task, which appears constantly
/// in async C# code.
/// </summary>
public class WorkTask
{
    public int TaskId { get; set; }
    public int ModuleId { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }

    public Module? Module { get; set; }
}
