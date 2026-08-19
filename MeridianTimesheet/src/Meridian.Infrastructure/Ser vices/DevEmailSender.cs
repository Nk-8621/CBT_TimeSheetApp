using Meridian.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace Meridian.Infrastructure.Services;

public class DevEmailSender(
	ILogger<DevEmailSender> logger,
	IConfiguration configuration) : IEmailSender
{
	public Task SendAsync(
		string toEmail,
		string subject,
		string body,
		CancellationToken ct = default)
	{
		var smtp = configuration.GetSection("SmtpSettings");

		using var client = new SmtpClient(
			smtp["Host"],
			int.Parse(smtp["Port"]!))
		{
			EnableSsl = bool.Parse(smtp["EnableSsl"]!),
			Credentials = new NetworkCredential(
				smtp["Username"],
				smtp["Password"])
		};

		using var message = new MailMessage
		{
			From = new MailAddress(
				smtp["FromEmail"]!,
				smtp["FromName"]),
			Subject = subject,
			Body = body,
			IsBodyHtml = true
		};

		message.To.Add(toEmail);

		client.Send(message);

		logger.LogInformation(
			"Email sent successfully to {ToEmail}",
			toEmail);

		return Task.CompletedTask;
	}
}