using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface IPasswordHasher
	{
		/// <summary>Returns a self-contained string encoding the iteration count,
		/// a random salt, and the derived hash — everything Verify needs later.</summary>
		string Hash(string password);

		bool Verify(string password, string storedHash);
	}
}
