namespace Meridian.Domain.Entities;

/// <summary>
/// A project methodology/category (Agile Scrum, AMS Support, Activate, HR,
/// Inside Sales, Marketing, Presales, ...) whose Level-1/Level-2 template
/// (see ProjectTypeModuleTemplate/ProjectTypeTaskTemplate) determines which
/// Modules and Tasks get auto-generated for a Project set to this type.
/// Renamed from the original placeholder "TaskCategory" concept - old
/// placeholder codes (consult/dev/bi/support/presales/train/admin) still
/// live here with no L1/L2 template rows; MasterDataService falls back to
/// the flat TaskTemplates.ByCategory list (a single "General" module) for
/// those until real data replaces them.
/// </summary>
public class ProjectType
{
    public int ProjectTypeId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }

    public ICollection<ProjectTypeModuleTemplate> ModuleTemplates { get; set; } = new List<ProjectTypeModuleTemplate>();
}
