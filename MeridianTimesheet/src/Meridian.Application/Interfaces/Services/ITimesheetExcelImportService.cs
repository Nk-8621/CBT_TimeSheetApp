using Meridian.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Meridian.Application.Interfaces.Services
{
	public interface ITimesheetExcelImportService
	{
		Task<ExcelImportResult> ImportWeekAsync(string employeeCode, DateOnly weekStartDate, Stream excelFileStream, CancellationToken ct = default);

		/// <summary>Builds the blank .xlsx template employees fill in and upload via ImportWeekAsync — same column layout that method reads.</summary>
		Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);

	}
}
