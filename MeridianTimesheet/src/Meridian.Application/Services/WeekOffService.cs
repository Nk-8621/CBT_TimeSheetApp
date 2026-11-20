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
/// WEEK OFFS (Nov 2026): Master Data -> Week Offs (Admin only).
/// Step 1 - create a client week off (client + week-off days + period).
/// Step 2 - assign employees to that client (on screen or by Excel upload).
/// The week-off days themselves are applied in DayTypeResolutionService.
/// </summary>
public class WeekOffService(
	IEmployeeWeekOffRepository weekOffRepository,
	IEmployeeRepository employeeRepository,
	IMasterDataRepository masterDataRepository) : IWeekOffService
{
	private static readonly string[] DateFormats = ["dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];

	public async Task<IReadOnlyList<ClientWeekOffDto>> GetAllAsync(CancellationToken ct = default)
	{
		var clientWeekOffs = await weekOffRepository.GetClientWeekOffsAsync(ct);
		var assignments = await weekOffRepository.GetAllAsync(ct);

		// Under each client week off: the employees assigned to that client
		// whose dates overlap that week-off period.
		return clientWeekOffs
			.Select(c => new ClientWeekOffDto(
				c.ClientWeekOffId,
				c.AccountId,
				c.Account?.Name ?? "",
				WeekOffDays.ToShortNames(WeekOffDays.Parse(c.WeekOffDays)),
				c.EffectiveFrom,
				c.EffectiveTo,
				assignments
					.Where(a => a.AccountId == c.AccountId && Overlaps(a.EffectiveFrom, a.EffectiveTo, c.EffectiveFrom, c.EffectiveTo))
					.Select(ToDto)
					.ToList()))
			.ToList();
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

	// ---- Client week offs ----

	public async Task CreateClientWeekOffAsync(SaveClientWeekOffRequest request, string createdByEmployeeCode, CancellationToken ct = default)
	{
		var days = await ValidateClientWeekOffAsync(request, excludeId: null, ct);
		var createdBy = await employeeRepository.GetByCodeAsync(createdByEmployeeCode, ct);

		await weekOffRepository.AddClientWeekOffAsync(new ClientWeekOff
		{
			AccountId = request.AccountId,
			WeekOffDays = WeekOffDays.Format(days),
			EffectiveFrom = request.EffectiveFrom,
			EffectiveTo = request.EffectiveTo,
			CreatedByEmployeeId = createdBy?.EmployeeId,
			CreatedAt = DateTime.UtcNow,
		}, ct);
		await weekOffRepository.SaveChangesAsync(ct);
	}

	public async Task UpdateClientWeekOffAsync(int id, SaveClientWeekOffRequest request, CancellationToken ct = default)
	{
		var record = await weekOffRepository.GetClientWeekOffByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(ClientWeekOff), id);
		if (record.AccountId != request.AccountId)
			throw new BusinessRuleException("The client can't be changed - add a new client week off instead.");

		var days = await ValidateClientWeekOffAsync(request, excludeId: id, ct);
		record.WeekOffDays = WeekOffDays.Format(days);
		record.EffectiveFrom = request.EffectiveFrom;
		record.EffectiveTo = request.EffectiveTo;
		record.UpdatedAt = DateTime.UtcNow;
		await weekOffRepository.SaveChangesAsync(ct);
	}

	public async Task DeleteClientWeekOffAsync(int id, CancellationToken ct = default)
	{
		var record = await weekOffRepository.GetClientWeekOffByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(ClientWeekOff), id);

		// The client's last week off can't go while employees are still assigned to it.
		var periodsForClient = await weekOffRepository.GetClientWeekOffsForAccountsAsync([record.AccountId], ct);
		if (periodsForClient.Count == 1 && await weekOffRepository.CountForAccountAsync(record.AccountId, ct) > 0)
			throw new BusinessRuleException("Employees are still assigned to this client. Remove them first, then delete the week off.");

		weekOffRepository.RemoveClientWeekOff(record);
		await weekOffRepository.SaveChangesAsync(ct);
	}

	// ---- Employee assignments ----

	public async Task AssignEmployeesAsync(AssignEmployeesRequest request, string createdByEmployeeCode, CancellationToken ct = default)
	{
		var codes = (request.EmployeeCodes ?? [])
			.Select(c => c.Trim())
			.Where(c => c.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (codes.Count == 0)
			throw new BusinessRuleException("Pick at least one employee.");

		await RequireClientWithWeekOffAsync(request.AccountId, ct);
		CheckDates(request.EffectiveFrom, request.EffectiveTo);

		// Check every employee first - nothing is saved if any one of them fails.
		var employees = new List<Employee>();
		var problems = new List<string>();
		foreach (var code in codes)
		{
			try
			{
				employees.Add(await ValidateEmployeeAsync(code, request.EffectiveFrom, request.EffectiveTo, excludeId: null, ct));
			}
			catch (BusinessRuleException ex)
			{
				problems.Add(ex.Message);
			}
		}
		if (problems.Count > 0)
			throw new BusinessRuleException(string.Join(" ", problems));

		var createdBy = await employeeRepository.GetByCodeAsync(createdByEmployeeCode, ct);
		foreach (var employee in employees)
		{
			await weekOffRepository.AddAsync(new EmployeeWeekOff
			{
				EmployeeId = employee.EmployeeId,
				AccountId = request.AccountId,
				EffectiveFrom = request.EffectiveFrom,
				EffectiveTo = request.EffectiveTo,
				CreatedByEmployeeId = createdBy?.EmployeeId,
				CreatedAt = DateTime.UtcNow,
			}, ct);
		}
		await weekOffRepository.SaveChangesAsync(ct);
	}

	public async Task UpdateEmployeeWeekOffAsync(int id, UpdateEmployeeWeekOffRequest request, CancellationToken ct = default)
	{
		var record = await weekOffRepository.GetByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(EmployeeWeekOff), id);
		var employee = await employeeRepository.GetByIdAsync(record.EmployeeId, ct)
			?? throw new EntityNotFoundException(nameof(Employee), record.EmployeeId);

		CheckDates(request.EffectiveFrom, request.EffectiveTo);
		await ValidateEmployeeAsync(employee.EmployeeCode, request.EffectiveFrom, request.EffectiveTo, excludeId: id, ct);

		record.EffectiveFrom = request.EffectiveFrom;
		record.EffectiveTo = request.EffectiveTo;
		record.UpdatedAt = DateTime.UtcNow;
		await weekOffRepository.SaveChangesAsync(ct);
	}

	public async Task DeleteEmployeeWeekOffAsync(int id, CancellationToken ct = default)
	{
		var record = await weekOffRepository.GetByIdAsync(id, ct)
			?? throw new EntityNotFoundException(nameof(EmployeeWeekOff), id);
		weekOffRepository.Remove(record);
		await weekOffRepository.SaveChangesAsync(ct);
	}

	// ---- Excel (employee assignments) ----

	public async Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
	{
		var clientWeekOffs = await weekOffRepository.GetClientWeekOffsAsync(ct);

		using var workbook = new XLWorkbook();
		var sheet = workbook.Worksheets.Add("Employees");
		var headers = new[] { "Employee Code", "Client Name", "From (DD-MM-YYYY)", "To (DD-MM-YYYY)" };
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
		sheet.Column(3).Width = 20;
		sheet.Column(4).Width = 20;
		sheet.SheetView.FreezeRows(1);

		// A second sheet explains how to fill it and lists the clients that
		// already have week offs set up (employees can only go to those).
		var help = workbook.Worksheets.Add("How to fill");
		var lines = new[]
		{
			"One row per employee per period.",
			"Employee Code - e.g. CBT1274.",
			"Client Name - must be one of the clients listed below (set up in Week Offs first).",
			"From - first day the employee works for this client (DD-MM-YYYY). Required.",
			"To - last day (DD-MM-YYYY). Leave empty if there is no end date.",
			"An employee's periods must not overlap, even across different clients.",
			"If any row has a problem, nothing is saved - fix the rows listed and upload again.",
		};
		help.Cell(1, 1).Value = "How to fill the Employees sheet";
		help.Cell(1, 1).Style.Font.Bold = true;
		for (var i = 0; i < lines.Length; i++)
			help.Cell(i + 3, 1).Value = lines[i];

		var row = lines.Length + 5;
		help.Cell(row, 1).Value = "Client Name";
		help.Cell(row, 2).Value = "Week Offs";
		help.Cell(row, 1).Style.Font.Bold = true;
		help.Cell(row, 2).Style.Font.Bold = true;
		foreach (var client in clientWeekOffs)
		{
			row++;
			help.Cell(row, 1).Value = client.Account?.Name ?? "";
			help.Cell(row, 2).Value = string.Join(", ", WeekOffDays.ToShortNames(WeekOffDays.Parse(client.WeekOffDays)))
				+ $" (from {client.EffectiveFrom:dd-MM-yyyy}"
				+ (client.EffectiveTo is DateOnly end ? $" to {end:dd-MM-yyyy})" : ")");
		}
		help.Column(1).Width = 90;
		help.Column(2).Width = 40;

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}

	public async Task<WeekOffImportResultDto> ImportAsync(Stream file, string createdByEmployeeCode, CancellationToken ct = default)
	{
		var errors = new List<string>();
		var validRows = new List<(Employee Employee, int AccountId, DateOnly From, DateOnly? To, int RowNumber)>();

		// Clients that have week offs set up - employees can only be assigned to these.
		var clientWeekOffs = await weekOffRepository.GetClientWeekOffsAsync(ct);

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
				if (code.Length == 0 && clientName.Length == 0 && excelRow.Cell(3).IsEmpty())
					continue; // empty line

				var from = ReadDate(excelRow.Cell(3));
				var to = ReadDate(excelRow.Cell(4));
				if (from is null) { errors.Add($"Row {rowNumber}: From date is missing or not in DD-MM-YYYY format."); continue; }
				if (!excelRow.Cell(4).IsEmpty() && to is null) { errors.Add($"Row {rowNumber}: To date is not in DD-MM-YYYY format."); continue; }

				var client = clientWeekOffs.FirstOrDefault(c => string.Equals(c.Account?.Name?.Trim(), clientName, StringComparison.OrdinalIgnoreCase));
				if (client is null) { errors.Add($"Row {rowNumber}: client \"{clientName}\" has no week offs set up."); continue; }

				try
				{
					CheckDates(from.Value, to);
					var employee = await ValidateEmployeeAsync(code, from.Value, to, excludeId: null, ct);

					// Rows for the same employee must not overlap each other either.
					var clash = validRows.FirstOrDefault(r => r.Employee.EmployeeId == employee.EmployeeId && Overlaps(r.From, r.To, from.Value, to));
					if (clash.Employee is not null)
					{
						errors.Add($"Row {rowNumber}: dates overlap row {clash.RowNumber} for {code}.");
						continue;
					}
					validRows.Add((employee, client.AccountId, from.Value, to, rowNumber));
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
				AccountId = row.AccountId,
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

	/// <summary>Checks a client week off and returns its days.</summary>
	private async Task<List<DayOfWeek>> ValidateClientWeekOffAsync(SaveClientWeekOffRequest request, int? excludeId, CancellationToken ct)
	{
		var account = await masterDataRepository.GetAccountByIdAsync(request.AccountId, ct);
		if (account is null || account.AccountType != AccountType.Customer)
			throw new BusinessRuleException("Pick a valid client.");

		var days = WeekOffDays.TryParse(request.WeekOffDays ?? []);
		if (days is null)
			throw new BusinessRuleException("Week off days must be day names like Fri,Sat.");
		if (days.Count == 0)
			throw new BusinessRuleException("Pick at least one week off day.");
		if (days.Count > 3)
			throw new BusinessRuleException("At most 3 week off days can be set.");

		CheckDates(request.EffectiveFrom, request.EffectiveTo);

		var existing = await weekOffRepository.GetClientWeekOffsForAccountsAsync([request.AccountId], ct);
		var clash = existing.FirstOrDefault(w => w.ClientWeekOffId != excludeId && Overlaps(w.EffectiveFrom, w.EffectiveTo, request.EffectiveFrom, request.EffectiveTo));
		if (clash is not null)
			throw new BusinessRuleException(
				$"{account.Name} already has week offs from {clash.EffectiveFrom:dd-MM-yyyy}" +
				(clash.EffectiveTo is DateOnly clashEnd ? $" to {clashEnd:dd-MM-yyyy}" : " (no end date)") +
				" - the dates overlap. Set an end date on that one first.");
		return days;
	}

	/// <summary>Checks one employee can be assigned for these dates and returns them.</summary>
	private async Task<Employee> ValidateEmployeeAsync(string employeeCode, DateOnly from, DateOnly? to, int? excludeId, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(employeeCode))
			throw new BusinessRuleException("Employee code is required.");
		var employee = await employeeRepository.GetByCodeAsync(employeeCode.Trim(), ct)
			?? throw new BusinessRuleException($"Employee {employeeCode} not found.");

		var existing = await weekOffRepository.GetAllForEmployeeAsync(employee.EmployeeId, ct);
		var clash = existing.FirstOrDefault(w => w.EmployeeWeekOffId != excludeId && Overlaps(w.EffectiveFrom, w.EffectiveTo, from, to));
		if (clash is not null)
			throw new BusinessRuleException(
				$"{employee.EmployeeCode} is already assigned from {clash.EffectiveFrom:dd-MM-yyyy}" +
				(clash.EffectiveTo is DateOnly clashEnd ? $" to {clashEnd:dd-MM-yyyy}" : " (no end date)") +
				" - the dates overlap.");
		return employee;
	}

	private async Task RequireClientWithWeekOffAsync(int accountId, CancellationToken ct)
	{
		var periods = await weekOffRepository.GetClientWeekOffsForAccountsAsync([accountId], ct);
		if (periods.Count == 0)
			throw new BusinessRuleException("Set up this client's week offs first.");
	}

	private static void CheckDates(DateOnly from, DateOnly? to)
	{
		if (to is DateOnly end && end < from)
			throw new BusinessRuleException("To date can't be before From date.");
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
		w.EffectiveFrom,
		w.EffectiveTo,
		w.CreatedBy?.FullName);
}
