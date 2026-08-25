using ClosedXML.Excel;
using Meridian.Application.DTOs;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;

namespace Meridian.Application.Services;

public class TimesheetExcelImportService(
	IMasterDataRepository masterDataRepository,
	ITimesheetService timesheetService) : ITimesheetExcelImportService
{
	// Column layout of the standard template — no "Week" column at all,
	// since the week is whichever one the employee already has open when
	// they upload (passed in separately, not read from the file).
	// A | Customer/Internal | B | Project | C | Module | D | Task
	// E | Billable (Y/N)    | F-L | Mon..Sun hours     | M | Notes

	public async Task<ExcelImportResult> ImportWeekAsync(string employeeCode, DateOnly weekStartDate, Stream excelFileStream, CancellationToken ct = default)
	{
		var errors = new List<string>();
		var imported = 0;

		// Loaded once, not per row — this file could have dozens of lines.
		var accounts = await masterDataRepository.GetAccountsAsync(ct);
		var projects = await masterDataRepository.GetProjectsAsync(ct);
		var modules = await masterDataRepository.GetModulesAsync(ct: ct);
		var tasks = await masterDataRepository.GetTasksAsync(ct: ct);

		using var workbook = new XLWorkbook(excelFileStream);
		var worksheet = workbook.Worksheet(1);

		// Find the header row by content rather than assuming it's row 1 —
		// the standard template has a title and instructions above it, and
		// even without that, people tend to add notes/titles to spreadsheets.
		var headerRow = worksheet.RowsUsed()
			.FirstOrDefault(r => string.Equals(r.Cell(1).GetString().Trim(), "Customer/Internal", StringComparison.OrdinalIgnoreCase));
		if (headerRow is null)
			return new ExcelImportResult(0, ["Couldn't find the header row — make sure column A somewhere has \"Customer/Internal\" as a header, matching the standard template."]);

		var dataRows = worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber());

		foreach (var row in dataRows)
		{
			var rowNumber = row.RowNumber();
			var accountName = row.Cell(1).GetString().Trim();
			var projectName = row.Cell(2).GetString().Trim();
			var moduleName = row.Cell(3).GetString().Trim();
			var taskName = row.Cell(4).GetString().Trim();
			var billableRaw = row.Cell(5).GetString().Trim();
			var note = row.Cell(13).GetString().Trim();

			if (string.IsNullOrWhiteSpace(projectName)) continue; // blank row — skip quietly, not an error

			// Reject-and-report rather than auto-create: a typo in a
			// spreadsheet should never silently become a new permanent
			// project/task in the system.
			var account = accounts.FirstOrDefault(a => string.Equals(a.Name, accountName, StringComparison.OrdinalIgnoreCase));
			if (account is null) { errors.Add($"Row {rowNumber}: customer/internal account '{accountName}' not found."); continue; }

			var project = projects.FirstOrDefault(p => p.AccountId == account.AccountId && string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
			if (project is null) { errors.Add($"Row {rowNumber}: project '{projectName}' not found under '{accountName}'."); continue; }

			var module = modules.FirstOrDefault(m => m.ProjectId == project.ProjectId && string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));
			if (module is null) { errors.Add($"Row {rowNumber}: module '{moduleName}' not found under project '{projectName}'."); continue; }

			var task = tasks.FirstOrDefault(t => t.ModuleId == module.ModuleId && string.Equals(t.Name, taskName, StringComparison.OrdinalIgnoreCase));
			if (task is null) { errors.Add($"Row {rowNumber}: task '{taskName}' not found under module '{moduleName}'."); continue; }

			var isBillable = billableRaw.Equals("Y", StringComparison.OrdinalIgnoreCase) || billableRaw.Equals("Yes", StringComparison.OrdinalIgnoreCase);

			var (hoursByDay, hourError) = ReadWeekHours(row, rowNumber);
			if (hourError is not null) { errors.Add(hourError); continue; }

			try
			{
				// ASSUMPTION — verify this matches your actual ITimesheetService
				// signature and adjust if it doesn't. This reuses the exact
				// same path the manual "Add task line" drawer uses, so the
				// 4h/task/day cap and every other existing rule applies here
				// automatically rather than needing to be duplicated.
				await timesheetService.AddEntryAsync(employeeCode, weekStartDate,
							new CreateTimeEntryRequest(project.ProjectId, module.ModuleId, task.TaskId, isBillable,
								string.IsNullOrWhiteSpace(note) ? null : note, hoursByDay!), ct);
				imported++;
			}
			catch (Exception ex)
			{
				errors.Add($"Row {rowNumber}: {ex.Message}");
			}
		}

		return new ExcelImportResult(imported, errors);
	}

	private static (decimal[]? HoursByDay, string? Error) ReadWeekHours(IXLRow row, int rowNumber)
	{
		var hoursByDay = new decimal[7];
		for (var i = 0; i < 7; i++)
		{
			var cellValue = row.Cell(6 + i).GetString().Trim();
			if (string.IsNullOrWhiteSpace(cellValue)) { hoursByDay[i] = 0; continue; }

			if (!decimal.TryParse(cellValue, out var hours) || hours < 0)
				return (null, $"Row {rowNumber}: '{cellValue}' isn't a valid number of hours.");

			hoursByDay[i] = hours;
		}
		return (hoursByDay, null);
	}
}