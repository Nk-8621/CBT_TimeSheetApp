using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Common
{
	public class JwtSettings
	{
		public string Secret { get; set; } = string.Empty;
		public string Issuer { get; set; } = "Meridian";
		public string Audience { get; set; } = "MeridianClients";
		public int ExpiryMinutes { get; set; } = 480; // 8 hours — one working day
	}
}
