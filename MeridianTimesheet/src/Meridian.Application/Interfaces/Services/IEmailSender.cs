using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface IEmailSender
	{
		Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
	}
}
