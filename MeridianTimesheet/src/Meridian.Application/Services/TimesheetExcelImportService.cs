using ClosedXML.Excel;
using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
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
	// E | Classification (Billable/NonBillable/PartialBillable)
	// F-L | Mon..Sun hours   | M | Notes | N | Billing Category (optional)

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
			var classificationRaw = row.Cell(5).GetString().Trim();
			var billingCategoryRaw = row.Cell(14).GetString().Trim();
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

			// Default to Billable so older templates saved before this column existed keep working unchanged.
			var classification = string.IsNullOrWhiteSpace(classificationRaw) ? "Billable" : classificationRaw;
			var billingCategory = string.IsNullOrWhiteSpace(billingCategoryRaw) ? null : billingCategoryRaw;

			try
			{
				BillingClassificationRules.Validate(classification, billingCategory);
			}
			catch (BusinessRuleException ex)
			{
				errors.Add($"Row {rowNumber}: {ex.Message}");
				continue;
			}

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
							new CreateTimeEntryRequest(project.ProjectId, module.ModuleId, task.TaskId, classification, billingCategory,
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

	/// <summary>Generates the blank .xlsx template — same column layout
	/// ImportWeekAsync reads (A: Customer/Internal … M: Notes), plus a title
	/// row and a short instructions row above the header so the header-row
	/// lookup in ImportWeekAsync (which searches for "Customer/Internal")
	/// still finds it correctly.</summary>
	public Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
	{
		using var workbook = new XLWorkbook();
		var sheet = workbook.Worksheets.Add("Timesheet Import");

		sheet.Cell(1, 1).Value = "Meridian Timesheet — Import Template";
		sheet.Cell(1, 1).Style.Font.Bold = true;
		sheet.Cell(1, 1).Style.Font.FontSize = 13;

		sheet.Cell(2, 1).Value = "Fill in one row per task line. Names must match Meridian exactly (Customer/Internal, Project, Module, Task). " +
			"Classification is Billable, NonBillable, or PartialBillable. Billing Category is optional (AMS/T&M/FB for Billable, OH for NonBillable, leave blank for PartialBillable). " +
			"Hours are per day, Monday through Sunday — leave a day blank for 0 hours. Do not change the header row.";
		sheet.Range(2, 1, 2, 14).Merge();
		sheet.Cell(2, 1).Style.Font.Italic = true;
		sheet.Cell(2, 1).Style.Font.FontSize = 10;
		sheet.Row(2).Height = 30;
		sheet.Cell(2, 1).Style.Alignment.WrapText = true;

		string[] headers =
		[
			"Customer/Internal", "Project", "Module", "Task", "Classification",
			"Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun", "Notes", "Billing Category",
		];
		var headerRow = sheet.Row(3);
		for (var i = 0; i < headers.Length; i++)
		{
			var cell = headerRow.Cell(i + 1);
			cell.Value = headers[i];
			cell.Style.Font.Bold = true;
			cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8, 0xEE, 0xF4);
			cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
		}

		// One blank example row beneath the header so the expected shape is obvious.
		sheet.Cell(4, 5).Value = "Billable";

		var classificationValidation = sheet.Column(5).SetDataValidation();
		classificationValidation.List("\"Billable,NonBillable,PartialBillable\"");
		classificationValidation.IgnoreBlanks = true;

		sheet.Column(1).Width = 22;
		sheet.Column(2).Width = 22;
		sheet.Column(3).Width = 18;
		sheet.Column(4).Width = 18;
		sheet.Column(5).Width = 16;
		for (var c = 6; c <= 12; c++) sheet.Column(c).Width = 7;
		sheet.Column(13).Width = 28;
		sheet.Column(14).Width = 16;

		sheet.SheetView.FreezeRows(3);

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return Task.FromResult(stream.ToArray());
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