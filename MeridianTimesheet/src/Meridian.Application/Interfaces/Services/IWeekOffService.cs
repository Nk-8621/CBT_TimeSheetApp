using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>WEEK OFFS (Nov 2026): Master Data -> Week Offs (Admin only, like the rest of Master Data).</summary>
public interface IWeekOffService
{
	/// <summary>Client week offs, each with its assigned employees.</summary>
	Task<IReadOnlyList<ClientWeekOffDto>> GetAllAsync(CancellationToken ct = default);
	Task<WeekOffOptionsDto> GetOptionsAsync(CancellationToken ct = default);

	// Client week offs
	Task CreateClientWeekOffAsync(SaveClientWeekOffRequest request, string createdByEmployeeCode, CancellationToken ct = default);
	Task UpdateClientWeekOffAsync(int id, SaveClientWeekOffRequest request, CancellationToken ct = default);
	Task DeleteClientWeekOffAsync(int id, CancellationToken ct = default);

	// Employee assignments
	Task AssignEmployeesAsync(AssignEmployeesRequest request, string createdByEmployeeCode, CancellationToken ct = default);
	Task UpdateEmployeeWeekOffAsync(int id, UpdateEmployeeWeekOffRequest request, CancellationToken ct = default);
	Task DeleteEmployeeWeekOffAsync(int id, CancellationToken ct = default);

	/// <summary>The blank Excel template for assigning employees in bulk.</summary>
	Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);

	/// <summary>Bulk employee assignment. Every row is checked first; if any
	/// row has a problem nothing is saved and the errors are returned.</summary>
	Task<WeekOffImportResultDto> ImportAsync(Stream file, string createdByEmployeeCode, CancellationToken ct = default);
}
