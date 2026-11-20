using System.Globalization;
using ClosedXML.Excel;
using Meridian.Application.Common;
using Meridian.Application.DTOs;
using Meridian.Application.Exceptions;
using Meridian.Application.Interfaces.Repositories;
using Meridian.Application.Interfaces.Services;
using Meridian.Domain.Entities;
using Meridian.Domain.Enums;

namespace Meridian.Application.Services;

/// <summary>
/// WEEK OFFS (Nov 2026): employee-specific week offs, managed by Admins
/// from Master Data -> Week Offs - one record at a time or by Excel upload.
/// The week-off days themselves are applied in DayTypeResolutionService.
/// </summary>
public class WeekOffService(
	IEmployeeWeekOffRepository weekOffRepository,
	IEmployeeRepository employeeRepository,
	IMasterDataRepository masterDataRepository) : IWeekOffService
{
	private static readonly string[] DateFormats = ["dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];

	public async Task<IReadOnlyList<EmployeeWeekOffDto>> GetAllAsync(CancellationToken ct = default)
	{
		var records = await weekOffRepository.GetAllAsync(ct);
		return records.Select(ToDto).ToList();
	}

	public async Task<WeekOffOptionsDto> GetOptionsAsync(CancellationToken ct = default)
	{
		var employees = (await employeeRepository.GetAllAsync(ct))
			.Where(e => e.IsActive)
			.OrderBy(e => e.FullName)
			.Select(e => new WeekOffOptionDto(e.EmployeeCode, $"{e.FullName} ({e.EmployeeCode})"))
			.ToList();
		var clients = (await GetClientsAsync(ct))
			.Select(a => new WeekOffOptionDto(a.AccountId.ToString(), a.Name))
			.ToList();
		return new WeekOffOptionsDto(employees, clients);
	}

	public async Task<EmployeeWeekOffDto> CreateAsync(SaveEmployeeWeekOffRequest request, string createdByEmployeeCode, CancellationToken ct = default)
	{
		var (employee, account, days) = await ValidateAsync(request.EmployeeCode, request.AccountId, request.WeekOffDays,
			request.EffectiveFrom, request.EffectiveTo, excludeId: null, ct);
		var createdBy = await employeeRepository.GetByCodeAsync(createdByEmployeeCode, ct);

		var record = new EmployeeWeekOff
		{
			EmployeeId = employee.EmployeeId,
			AccountId = account.AccountId,
			WeekOffDays = WeekOffDays.Format(days),
			EffectiveFrom = request.EffectiveFrom,
			EffectiveTo = request.EffectiveTo,
			CreatedByEmployeeId = createdBy?.EmployeeId,
			CreatedAt = DateTime.UtcNow,
		};
		await weekOffRepository.AddAsync(record, ct);
		await weekOffRepository.SaveChangesAsync(ct);

		record.Employee = employee;
		record.Account = account;
		record.CreatedBy = createdBy;
		return ToDto(record);
	}

	public async Task<EmployeeWeekOffDto> UpdateAsync(int id, SaveEmployeeWeekOffRequest request, CancellationToken ct = default)
	{
		var record = await weekOffRepository.GetByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(EmployeeWeekOff), id);

		var (employee, account, days) = await ValidateAsync(request.EmployeeCode, request.AccountId, request.WeekOffDays,
			request.EffectiveFrom, request.EffectiveTo, excludeId: id, ct);

		record.EmployeeId = employee.EmployeeId;
		record.AccountId = account.AccountId;
		record.WeekOffDays = WeekOffDays.Format(days);
		record.EffectiveFrom = request.EffectiveFrom;
		record.EffectiveTo = request.EffectiveTo;
		record.UpdatedAt = DateTime.UtcNow;
		await weekOffRepository.SaveChangesAsync(ct);

		var createdBy = record.CreatedByEmployeeId is int createdById ? await employeeRepository.GetByIdAsync(createdById, ct) : null;
		record.Employee = employee;
		record.Account = account;
		record.CreatedBy = createdBy;
		return ToDto(record);
	}

	public async Task DeleteAsync(int id, CancellationToken ct = default)
	{
		var record = await weekOffRepository.GetByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(EmployeeWeekOff), id);
		weekOffRepository.Remove(record);
		await weekOffRepository.SaveChangesAsync(ct);
	}

	// ---- Excel ----

	public async Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
	{
		var clients = await GetClientsAsync(ct);

		using var workbook = new XLWorkbook();
		var sheet = workbook.Worksheets.Add("Week Offs");
		var headers = new[] { "Employee Code", "Client Name", "Week Off Days", "From (DD-MM-YYYY)", "To (DD-MM-YYYY)" };
		for (var col = 1; col <= headers.Length; col++)
		{
			var cell = sheet.Cell(1, col);
			cell.Value = headers[col - 1];
			cell.Style.Font.Bold = true;
			cell.Style.Font.FontColor = XLColor.White;
			cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#173A5E");
		}
		sheet.Column(1).Width = 16;
		sheet.Column(2).Width = 30;
		sheet.Column(3).Width = 18;
		sheet.Column(4).Width = 20;
		sheet.Column(5).Width = 20;
		sheet.SheetView.FreezeRows(1);

		// A second sheet explains how to fill it and lists the valid client names.
		var help = workbook.Worksheets.Add("How to fill");
		var lines = new[]
		{
			"One row per employee per period.",
			"Employee Code - e.g. CBT1274.",
			"Client Name - must match one of the names listed below exactly.",
			"Week Off Days - short day names separated by commas, e.g. Fri,Sat or Sun.",
			"From - first day these week offs apply (DD-MM-YYYY). Required.",
			"To - last day they apply (DD-MM-YYYY). Leave empty if there is no end date.",
			"Periods for the same employee must not overlap.",
			"If any row has a problem, nothing is saved - fix the rows listed and upload again.",
		};
		help.Cell(1, 1).Value = "How to fill the Week Offs sheet";
		help.Cell(1, 1).Style.Font.Bold = true;
		for (var i = 0; i < lines.Length; i++)
			help.Cell(i + 3, 1).Value = lines[i];

		var row = lines.Length + 5;
		help.Cell(row, 1).Value = "Client names";
		help.Cell(row, 1).Style.Font.Bold = true;
		foreach (var client in clients)
			help.Cell(++row, 1).Value = client.Name;
		help.Column(1).Width = 90;

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}

	public async Task<WeekOffImportResultDto> ImportAsync(Stream file, string createdByEmployeeCode, CancellationToken ct = default)
	{
		var errors = new List<string>();
		var validRows = new List<(Employee Employee, Account Account, List<DayOfWeek> Days, DateOnly From, DateOnly? To, int RowNumber)>();

		XLWorkbook workbook;
		try
		{
			workbook = new XLWorkbook(file);
		}
		catch (Exception)
		{
			throw new BusinessRuleException("This file could not be read. Please upload the .xlsx template.");
		}

		using (workbook)
		{
			var sheet = workbook.Worksheet(1);
			foreach (var excelRow in sheet.RowsUsed())
			{
				var rowNumber = excelRow.RowNumber();
				if (rowNumber == 1) continue; // header

				var code = excelRow.Cell(1).GetString().Trim();
				var clientName = excelRow.Cell(2).GetString().Trim();
				var daysText = excelRow.Cell(3).GetString().Trim();
				if (code.Length == 0 && clientName.Length == 0 && daysText.Length == 0 && excelRow.Cell(4).IsEmpty())
					continue; // empty line

				var from = ReadDate(excelRow.Cell(4));
				var to = ReadDate(excelRow.Cell(5));

				if (from is null) { errors.Add($"Row {rowNumber}: From date is missing or not in DD-MM-YYYY format."); continue; }
				if (!excelRow.Cell(5).IsEmpty() && to is null) { errors.Add($"Row {rowNumber}: To date is not in DD-MM-YYYY format."); continue; }

				var clients = await GetClientsAsync(ct);
				var account = clients.FirstOrDefault(a => string.Equals(a.Name.Trim(), clientName, StringComparison.OrdinalIgnoreCase));
				if (account is null) { errors.Add($"Row {rowNumber}: client \"{clientName}\" not found."); continue; }

				try
				{
					var (employee, _, days) = await ValidateAsync(code, account.AccountId, daysText.Split(','), from.Value, to, excludeId: null, ct);

					// Rows in the same file for the same employee must not overlap each other either.
					var clash = validRows.FirstOrDefault(r => r.Employee.EmployeeId == employee.EmployeeId && Overlaps(r.From, r.To, from.Value, to));
					if (clash.Employee is not null)
					{
						errors.Add($"Row {rowNumber}: dates overlap row {clash.RowNumber} for {code}.");
						continue;
					}
					validRows.Add((employee, account, days, from.Value, to, rowNumber));
				}
				catch (BusinessRuleException ex)
				{
					errors.Add($"Row {rowNumber}: {ex.Message}");
				}
			}
		}

		if (errors.Count > 0) return new WeekOffImportResultDto(0, errors);
		if (validRows.Count == 0) return new WeekOffImportResultDto(0, ["The file has no rows to upload."]);

		var createdBy = await employeeRepository.GetByCodeAsync(createdByEmployeeCode, ct);
		foreach (var row in validRows)
		{
			await weekOffRepository.AddAsync(new EmployeeWeekOff
			{
				EmployeeId = row.Employee.EmployeeId,
				AccountId = row.Account.AccountId,
				WeekOffDays = WeekOffDays.Format(row.Days),
				EffectiveFrom = row.From,
				EffectiveTo = row.To,
				CreatedByEmployeeId = createdBy?.EmployeeId,
				CreatedAt = DateTime.UtcNow,
			}, ct);
		}
		await weekOffRepository.SaveChangesAsync(ct);
		return new WeekOffImportResultDto(validRows.Count, []);
	}

	// ---- Shared helpers ----

	/// <summary>The checks for one record, used by the screen and the Excel upload alike.</summary>
	private async Task<(Employee Employee, Account Account, List<DayOfWeek> Days)> ValidateAsync(
		string employeeCode, int accountId, IEnumerable<string> weekOffDays,
		DateOnly from, DateOnly? to, int? excludeId, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(employeeCode))
			throw new BusinessRuleException("Employee code is required.");
		var employee = await employeeRepository.GetByCodeAsync(employeeCode.Trim(), ct)
			?? throw new BusinessRuleException($"employee {employeeCode} not found.");

		var account = await masterDataRepository.GetAccountByIdAsync(accountId, ct);
		if (account is null || account.AccountType != AccountType.Customer)
			throw new BusinessRuleException("Pick a valid client.");

		var days = WeekOffDays.TryParse(weekOffDays);
		if (days is null)
			throw new BusinessRuleException("Week off days must be day names like Fri,Sat.");
		if (days.Count == 0)
			throw new BusinessRuleException("Pick at least one week off day.");
		if (days.Count > 3)
			throw new BusinessRuleException("At most 3 week off days can be set.");

		if (to is DateOnly end && end < from)
			throw new BusinessRuleException("To date can't be before From date.");

		var existing = await weekOffRepository.GetAllForEmployeeAsync(employee.EmployeeId, ct);
		var clash = existing.FirstOrDefault(w => w.EmployeeWeekOffId != excludeId && Overlaps(w.EffectiveFrom, w.EffectiveTo, from, to));
		if (clash is not null)
			throw new BusinessRuleException(
				$"{employee.EmployeeCode} already has week offs from {clash.EffectiveFrom:dd-MM-yyyy}" +
				(clash.EffectiveTo is DateOnly clashEnd ? $" to {clashEnd:dd-MM-yyyy}" : " (no end date)") +
				" - the dates overlap.");

		return (employee, account, days);
	}

	/// <summary>Do two date ranges share at least one day? A null end means "no end date".</summary>
	private static bool Overlaps(DateOnly fromA, DateOnly? toA, DateOnly fromB, DateOnly? toB) =>
		(toB is null || fromA <= toB) && (toA is null || fromB <= toA);

	private async Task<List<Account>> GetClientsAsync(CancellationToken ct) =>
		(await masterDataRepository.GetAccountsAsync(ct))
			.Where(a => a.AccountType == AccountType.Customer)
			.OrderBy(a => a.Name)
			.ToList();

	/// <summary>An Excel cell as a date - either a real date cell or text like 01-12-2026.</summary>
	private static DateOnly? ReadDate(IXLCell cell)
	{
		if (cell.IsEmpty()) return null;
		if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
		var text = cell.GetString().Trim();
		return DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
			? date
			: null;
	}

	private static EmployeeWeekOffDto ToDto(EmployeeWeekOff w) => new(
		w.EmployeeWeekOffId,
		w.Employee?.EmployeeCode ?? "",
		w.Employee?.FullName ?? "",
		w.AccountId,
		w.Account?.Name ?? "",
		WeekOffDays.ToShortNames(WeekOffDays.Parse(w.WeekOffDays)),
		w.EffectiveFrom,
		w.EffectiveTo,
		w.CreatedBy?.FullName);
}
