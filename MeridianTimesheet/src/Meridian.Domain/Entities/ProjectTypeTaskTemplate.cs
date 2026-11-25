namespace Meridian.Domain.Entities;

/// <summary>
/// A Level-2 task name (e.g. "User Stories", "Code Build") within a
/// ProjectTypeModuleTemplate. One real WorkTask gets generated per
/// template row here, under the Module generated from its parent.
/// </summary>
public class ProjectTypeTaskTemplate
{
    public int ProjectTypeTaskTemplateId { get; set; }
    public int ProjectTypeModuleTemplateId { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public ProjectTypeModuleTemplate? ModuleTemplate { get; set; }
}
