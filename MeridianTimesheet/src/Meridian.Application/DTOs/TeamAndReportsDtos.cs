using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.DTOs
{
	public record TeamComplianceRowDto(
	string EmployeeCode,
	string FullName,
	string Designation,
	string Status, // NotStarted, Draft, PendingL1, PendingL2, Approved, Rejected
	decimal TotalHours,
	bool HasLogged
);

	public record ProjectHoursReportRowDto(
		int ProjectId,
		string ProjectCode,
		string ProjectName,
		string AccountName,
		decimal BillableHours,
		decimal NonBillableHours,
		decimal TotalHours,
		int EmployeeCount
	);
}
