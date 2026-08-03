using Meridian.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface IReportsService
	{
		/// <summary>Hours broken down by project, for one week, summed across the
		/// given set of employees. The caller (controller) decides which
		/// employees are in scope — direct reports for a manager, everyone for
		/// an Admin — this method just aggregates whatever list it's given.</summary>
		Task<IReadOnlyList<ProjectHoursReportRowDto>> GetProjectHoursAsync(
			IReadOnlyList<int> employeeIds, DateOnly weekStart, CancellationToken ct = default);
	}
}
