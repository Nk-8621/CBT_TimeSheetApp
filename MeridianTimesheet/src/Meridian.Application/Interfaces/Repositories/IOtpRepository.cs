using Meridian.Domain.Entities;
using Meridian.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Repositories
{
	public interface IOtpRepository
	{
		/// <summary>The most recently issued OTP for this employee/purpose,
		/// regardless of whether it's since been used, invalidated, or expired —
		/// the service layer inspects that state itself (e.g. to enforce the
		/// resend cooldown, or to know there's nothing left to verify against).
		/// Since every issuance creates a new row rather than reusing one,
		/// "most recent" always correctly identifies whichever OTP is currently
		/// relevant.</summary>
		Task<Otp?> GetMostRecentAsync(int employeeId, OtpPurpose purpose, CancellationToken ct = default);

		Task AddAsync(Otp otp, CancellationToken ct = default);
		Task SaveChangesAsync(CancellationToken ct = default);
	}
}
