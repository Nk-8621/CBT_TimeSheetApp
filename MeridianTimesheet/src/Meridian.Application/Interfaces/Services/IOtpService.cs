using Meridian.Domain.Enums;

namespace Meridian.Application.Interfaces.Services;

public enum OtpVerificationOutcome
{
	Success,
	InvalidCode,
	Expired,
	TooManyAttempts,
	NoActiveOtp,
}

public interface IOtpService
{
	/// <summary>Called when login determines an OTP is needed. Issues a new
	/// OTP unless one is already valid and outstanding — avoids sending a
	/// fresh email on every retried login attempt.</summary>
	Task RequestAsync(int employeeId, string email, OtpPurpose purpose, CancellationToken ct = default);

	/// <summary>Called by the explicit "Resend code" action. Always issues a
	/// fresh OTP (invalidating whatever was outstanding), but still enforces
	/// the resend cooldown — throws BusinessRuleException if called too soon.</summary>
	Task ResendAsync(int employeeId, string email, OtpPurpose purpose, CancellationToken ct = default);

	Task<OtpVerificationOutcome> VerifyAsync(int employeeId, OtpPurpose purpose, string plainCode, CancellationToken ct = default);
}