using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public record IssuedToken(string Token, DateTime ExpiresAtUtc);

	public interface IJwtTokenService
	{
		IssuedToken GenerateToken(string employeeCode, bool isAdmin);
	}
}
