namespace Meridian.Domain.Entities;

/// <summary>
/// A Project Type (e.g. Agile/Scrum, AMS Support, Presales) drives which
/// standard Module/Task template gets applied to a new Project of that
/// type, and which task-name template a Module's tasks are generated from.
/// NOTE: this file is still named TaskCategory.cs for historical reasons
/// (the class used to be called TaskCategory) - safe to rename the file to
/// ProjectType.cs whenever convenient, it doesn't affect compilation.
/// </summary>
public class ProjectType
{
    public int ProjectTypeId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }

    public ICollection<ProjectTypeModuleTemplate> ModuleTemplates { get; set; } = new List<ProjectTypeModuleTemplate>();
}

/// <summary>A Level-1 (Module) template row belonging to a Project Type -
/// when a new Project of that type is created, one Module is generated per
/// template row, in SortOrder.</summary>
public class ProjectTypeModuleTemplate
{
    public int ProjectTypeModuleTemplateId { get; set; }
    public int ProjectTypeId { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public ProjectType? ProjectType { get; set; }
    public ICollection<ProjectTypeTaskTemplate> TaskTemplates { get; set; } = new List<ProjectTypeTaskTemplate>();
}

/// <summary>A Level-2 (Task) template row belonging to one Module template -
/// when a Module is generated from its template, one Task is generated per
/// template row, in SortOrder.</summary>
public class ProjectTypeTaskTemplate
{
    public int ProjectTypeTaskTemplateId { get; set; }
    public int ProjectTypeModuleTemplateId { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public ProjectTypeModuleTemplate? ModuleTemplate { get; set; }
}
