using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Common
{
	/// <summary>
	/// The same task-name-by-category templates used to originally seed the
	/// placeholder project data. Kept here (not just in the one-off Python seed
	/// script) so the running application can replicate the same behavior the
	/// original wireframe had: creating a new project auto-creates a starter
	/// "General" module pre-populated with a standard task list, so the project
	/// is immediately usable on the timesheet grid without a second setup step.
	/// </summary>
	public static class TaskTemplates
	{
		public static readonly IReadOnlyDictionary<string, string[]> ByCategory = new Dictionary<string, string[]>
		{
			["consult"] = ["Requirement Gathering", "As-Is Process Study", "Gap Analysis", "Workshop / Client Discussion", "Documentation", "Review & Rework"],
			["dev"] = ["Technical Design", "Development", "Unit Testing", "Bug Fix / Rework", "Code Review", "Deployment & Release"],
			["bi"] = ["Data Modelling", "Report Development", "DAX / Measure Build", "Validation & UAT Support", "Rework"],
			["support"] = ["Incident Resolution", "Root Cause Analysis", "Enhancement Build", "Monthly Report Preparation"],
			["presales"] = ["Solution Study", "Proposal / Deck Preparation", "Effort Estimation", "Internal Review", "Client Presentation"],
			["train"] = ["Self Study", "Instructor-led Session", "Certification / Assessment", "Knowledge Sharing Session"],
			["admin"] = ["Team Meeting", "Timesheet & Reporting", "Interview Panel", "Appraisal Activity", "General Administration"],
		};
	}
}
