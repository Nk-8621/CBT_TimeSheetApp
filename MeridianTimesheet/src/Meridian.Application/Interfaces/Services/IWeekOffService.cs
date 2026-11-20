using Meridian.Application.DTOs;

namespace Meridian.Application.Interfaces.Services;

/// <summary>WEEK OFFS (Nov 2026): Master Data -> Week Offs (Admin only, like the rest of Master Data).</summary>
public interface IWeekOffService
{
	Task<IReadOnlyList<EmployeeWeekOffDto>> GetAllAsync(CancellationToken ct = default);
	Task<WeekOffOptionsDto> GetOptionsAsync(CancellationToken ct = default);
	Task<EmployeeWeekOffDto> CreateAsync(SaveEmployeeWeekOffRequest request, string createdByEmployeeCode, CancellationToken ct = default);
	Task<EmployeeWeekOffDto> UpdateAsync(int id, SaveEmployeeWeekOffRequest request, CancellationToken ct = default);
	Task DeleteAsync(int id, CancellationToken ct = default);

	/// <summary>The blank Excel template for bulk upload.</summary>
	Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);

	/// <summary>Bulk upload. Every row is checked first; if any row has a
	/// problem nothing is saved and the errors are returned.</summary>
	Task<WeekOffImportResultDto> ImportAsync(Stream file, string createdByEmployeeCode, CancellationToken ct = default);
}
