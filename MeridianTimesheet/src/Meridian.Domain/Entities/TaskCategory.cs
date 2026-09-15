namespace Meridian.Domain.Entities;

// This file used to define the ProjectType / ProjectTypeModuleTemplate /
// ProjectTypeTaskTemplate classes under their old working name
// ("TaskCategory"). Those now live in their own files - ProjectType.cs,
// ProjectTypeModuleTemplate.cs, ProjectTypeTaskTemplate.cs. Keeping the
// classes here too (as a leftover from the Project_module_changes merge)
// does not compile: CS0101, "the namespace already contains a definition
// for 'ProjectType'" (and the same for the other two).
//
// Left as an empty stub rather than deleted outright - I can edit files on
// your machine through the device bridge but can't delete them, so this
// file still exists on disk. Safe to `git rm` this file entirely whenever
// convenient; nothing in the codebase references Meridian.Domain.Entities
// from a file named TaskCategory.cs, only from the files named above.
