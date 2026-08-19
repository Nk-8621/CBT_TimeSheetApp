using Meridian.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Domain.Entities
{
	public class Otp
	{
		public int OtpId { get; set; }
		public int EmployeeId { get; set; }
		public Employee? Employee { get; set; }

		public OtpPurpose Purpose { get; set; }

		/// <summary>Salted SHA-256 of the 6-digit code — the raw code is never stored.</summary>
		public string OtpHash { get; set; } = string.Empty;

		public DateTime ExpiresAt { get; set; }
		public int AttemptCount { get; set; }
		public bool IsUsed { get; set; }

		/// <summary>Set when a newer OTP for the same employee/purpose supersedes
		/// this one, so at most one OTP is ever valid at a time.</summary>
		public DateTime? InvalidatedAt { get; set; }

		public DateTime CreatedAt { get; set; }
	}
}
