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
	}
}
