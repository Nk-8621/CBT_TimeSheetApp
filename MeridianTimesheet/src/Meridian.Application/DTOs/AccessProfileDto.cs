using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.DTOs
{
	public record AccessProfileDto(
	string EmployeeCode,
	bool IsAdmin,
	bool RequiresTimesheet,
	bool IsLevel1ApproverForSomeone,
	bool IsLevel2ApproverForSomeone,
	IReadOnlyList<string> NavKeys
);
}
