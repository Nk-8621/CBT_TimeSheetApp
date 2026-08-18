using Meridian.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface IAuthenticationService
	{
		/// <summary>Null means invalid employee code or password. Deliberately
		/// not distinguishing "unknown employee", "no login access granted", and
		/// "wrong password" — the controller maps all three to one generic 401,
		/// so none of them can be used to enumerate real employee codes.</summary>
		Task<LoginResult?> LoginAsync(string employeeCode, string password, CancellationToken ct = default);

		/// <summary>Throws BusinessRuleException with a specific, user-facing
		/// message on any validation or OTP failure. Safe to be specific here —
		/// unlike login, the caller already proved they know a valid password
		/// before reaching this step, so there's no enumeration risk.</summary>
		Task<LoginResult> VerifyFirstLoginOtpAndSetPasswordAsync(
			string employeeCode, string otpCode, string newPassword, string confirmNewPassword, CancellationToken ct = default);

		Task ResendFirstLoginOtpAsync(string employeeCode, CancellationToken ct = default);
	}
}
