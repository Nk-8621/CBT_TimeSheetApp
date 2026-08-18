using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public record GeneratedOtp(string PlainCode, string Hash);

	public interface IOtpGenerator
	{
		GeneratedOtp Generate();
		bool Verify(string plainCode, string storedHash);
	}
}
