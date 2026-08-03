namespace Meridian.Domain.Entities;

/// <summary>
/// consult, dev, bi, support, presales, train, admin — determines which
/// task-name template a Module's tasks are generated from.
/// </summary>
public class TaskCategory
{
    public int TaskCategoryId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}
