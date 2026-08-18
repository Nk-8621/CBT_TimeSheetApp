using Meridian.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Meridian.Infrastructure.Services;

/// <summary>
/// Dev-phase stand-in — logs the email server-side instead of actually
/// sending it, exactly like DevAuthenticationHandler stands in for real
/// Entra login elsewhere in this app. This is NOT wired to any real email
/// provider. Swap for a real SMTP/SendGrid implementation once credentials
/// exist; nothing else in the OTP flow needs to change when that happens.
///
/// Logged at Information level so it's visible in the console during local
/// testing — this is a deliberate, temporary exception to "never log an
/// OTP" for dev convenience only. Remove or gate this behind an
/// environment check before this ever runs anywhere but a developer machine.
/// </summary>
public class DevEmailSender(ILogger<DevEmailSender> logger) : IEmailSender
{
	public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
	{
		logger.LogInformation(
			"[DEV EMAIL — not actually sent] To: {ToEmail} | Subject: {Subject}\n{Body}",
			toEmail, subject, body);
		return Task.CompletedTask;
	}
}