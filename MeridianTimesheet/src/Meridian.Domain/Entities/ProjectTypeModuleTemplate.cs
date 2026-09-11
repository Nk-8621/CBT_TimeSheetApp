namespace Meridian.Domain.Entities;

/// <summary>
/// A Level-1 grouping (e.g. "Design", "Build", "Testing") within a
/// ProjectType's template. When a Project is created/updated with this
/// ProjectType, one real Module gets generated per template row here.
/// </summary>
public class ProjectTypeModuleTemplate
{
    public int ProjectTypeModuleTemplateId { get; set; }
    public int ProjectTypeId { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public ProjectType? ProjectType { get; set; }
    public ICollection<ProjectTypeTaskTemplate> TaskTemplates { get; set; } = new List<ProjectTypeTaskTemplate>();
}
