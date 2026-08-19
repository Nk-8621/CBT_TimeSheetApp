using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Common
{
	public class OtpSettings
	{
		public int ExpiryMinutes { get; set; } = 10;
		public int MaxVerificationAttempts { get; set; } = 5;
		public int ResendCooldownSeconds { get; set; } = 60;
	}
}
