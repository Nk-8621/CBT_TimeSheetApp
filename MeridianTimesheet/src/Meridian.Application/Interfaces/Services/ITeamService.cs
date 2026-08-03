using Meridian.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface ITeamService
	{
		/// <summary>This manager's direct reports only.</summary>
		Task<IReadOnlyList<TeamComplianceRowDto>> GetComplianceForManagerAsync(string managerEmployeeCode, DateOnly weekStart, CancellationToken ct = default);

		/// <summary>Every employee in the organization — callers must check
		/// Admin status themselves before calling this (enforced at the
		/// controller, same pattern as the rest of the API).</summary>
		Task<IReadOnlyList<TeamComplianceRowDto>> GetComplianceForAllAsync(DateOnly weekStart, CancellationToken ct = default);
	}
}
