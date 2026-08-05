using Meridian.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface IAccessControlService
	{
		Task<AccessProfileDto> GetAccessProfileAsync(string employeeCode, CancellationToken ct = default);
	}
}
