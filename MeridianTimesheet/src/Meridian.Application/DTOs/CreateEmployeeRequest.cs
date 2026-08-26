using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.DTOs
{
	public record CreateEmployeeRequest(
	string FullName,
	string Email,
	string Designation,
	string ManagerEmployeeCode,
	int DepartmentId,
	bool IsExternal,
	string? EmployeeCode = null // required when IsExternal is false; ignored (auto-generated) when true
	);

	public record SetPrimaryAccountRequest(int? AccountId);
}
