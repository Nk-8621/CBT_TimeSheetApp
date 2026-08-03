using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.DTOs
{
	public record NotificationDto(
	int Id,
	string Title,
	string Message,
	string Kind, // Warning, Info, Risk
	DateTime CreatedAt,
	DateTime? ReadAt,
	bool IsBroadcast
);
}
