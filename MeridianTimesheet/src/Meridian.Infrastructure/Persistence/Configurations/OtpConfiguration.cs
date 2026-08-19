using Meridian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Infrastructure.Persistence.Configurations
{
	public class OtpConfiguration : IEntityTypeConfiguration<Otp>
	{
		public void Configure(EntityTypeBuilder<Otp> builder)
		{
			builder.ToTable("Carbynetech_Otp");
			builder.HasKey(o => o.OtpId);

			// Without this, EF Core's default convention stores the enum as an
			// int — but the actual column is NVARCHAR(20). This explicit
			// conversion is what makes the two agree.
			builder.Property(o => o.Purpose)
				.HasConversion<string>()
				.HasMaxLength(20)
				.IsRequired();

			builder.Property(o => o.OtpHash)
				.HasMaxLength(200)
				.IsRequired();

			builder.HasOne(o => o.Employee)
				.WithMany()
				.HasForeignKey(o => o.EmployeeId)
				.OnDelete(DeleteBehavior.Restrict);
		}
	}
}
